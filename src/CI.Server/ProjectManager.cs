using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Helium.Pipeline;
using Helium.Util;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Nito.AsyncEx;

namespace Helium.CI.Server
{
    public sealed class ProjectManager : IProjectManager
    {
        private ProjectManager(string projectsDir, IJobQueue jobQueue, IDictionary<Guid, IProject> projects, CancellationToken cancellationToken) {
            this.projectsDir = projectsDir;
            this.jobQueue = jobQueue;
            this.projects = new ConcurrentDictionary<Guid, IProject>(projects);
            this.cancellationToken = cancellationToken;
        }

        private readonly string projectsDir;
        private readonly IJobQueue jobQueue;
        private readonly ConcurrentDictionary<Guid, IProject> projects;
        private readonly CancellationToken cancellationToken;

        public IProject? GetProject(Guid id) => projects.TryGetValue(id, out var project) ? project : null;

        public IReadOnlyCollection<IProject> Projects => new ReadOnlyCollectionNoList<IProject>(projects.Values);

        public async Task<IProject> AddProject(ProjectConfig config) {
            Guid id;
            Project project;
            do {
                cancellationToken.ThrowIfCancellationRequested();
                id = Guid.NewGuid();
                var dir = Path.Combine(projectsDir, id.ToString());
                project = new Project(dir, id, jobQueue, config);
            } while(!projects.TryAdd(id, project));

            await project.WriteConfig(null, cancellationToken);

            return project;
        }

        public async Task RemoveProject(IProject project) {
            var project2 = (Project)project;
            if(!projects.TryRemove(project2.Id, out _)) {
                return;
            }
            
            Directory.Delete(project2.ProjectDir);
        }

        public static async Task<IProjectManager> Load(string projectsDir, IJobQueue jobQueue, CancellationToken cancellationToken) {
            var projects = new Dictionary<Guid, IProject>();

            Directory.CreateDirectory(projectsDir);

            foreach(var projectDir in Directory.EnumerateDirectories(projectsDir)) {
                cancellationToken.ThrowIfCancellationRequested();
                
                if(!Guid.TryParse(Path.GetFileName(projectDir), out var id)) {
                    continue;
                }
                
                var configStr = await File.ReadAllTextAsync(Path.Combine(projectDir, "project.json"), cancellationToken);
                var config = JsonConvert.DeserializeObject<ProjectConfig>(configStr);
                
                var project = new Project(Path.GetFullPath(projectDir), id, jobQueue, config);
                projects[id] = project;
            }
            
            return new ProjectManager(projectsDir, jobQueue, projects, cancellationToken);
        }

        private sealed class Project : IProject
        {
            public Project(string dir, Guid id, IJobQueue jobQueue, ProjectConfig config) {
                ProjectDir = dir;
                this.jobQueue = jobQueue;
                Id = id;
                Config = config;
            }

            private readonly AsyncLock projectLock = new AsyncLock();
            private readonly IJobQueue jobQueue;
            
            public ProjectConfig Config { get; private set; }
            public string ProjectDir { get; }
            public Guid Id { get; }

            public async Task WriteConfig(ProjectConfig? config, CancellationToken cancellationToken) {
                if(config == null) config = Config;
                var tmpFile = Path.Combine(ProjectDir, "project.json.tmp");
                var configStr = JsonConvert.SerializeObject(config);
                
                Directory.CreateDirectory(ProjectDir);
                
                await FileUtil.WriteAllTextToDiskAsync(tmpFile, configStr, Encoding.UTF8, cancellationToken);
                File.Move(tmpFile, Path.Combine(ProjectDir, "project.json"), true);
            }
            
            public async Task UpdateConfig(ProjectConfig config, CancellationToken cancellationToken) {
                await WriteConfig(config, cancellationToken);
                using(await projectLock.LockAsync(cancellationToken)) {
                    Config = config;
                }
            }

            public async Task<PipelineLoader> GetPipelineLoader(CancellationToken cancellationToken) {
                var script = await GetPipelineScript(cancellationToken);
                return PipelineLoader.Create(script);
            }

            public async Task<IReadOnlyDictionary<BuildJob, IJobStatus>> StartBuild(PipelineInfo pipeline) {
                string buildDir;
                using(await projectLock.LockAsync()) {
                    var buildsDir = Path.Combine(ProjectDir, "builds");
                    Directory.CreateDirectory(buildsDir);
                    int lastBuildNumber = Directory.GetDirectories(buildsDir, "build*")
                        .Select(dir =>
                            int.TryParse(Path.GetFileName(dir).Substring(5), out var num) ? (int?) num : null)
                        .DefaultIfEmpty(null)
                        .Max()
                        ?? 0;

                    buildDir = Path.Combine(buildsDir, "build" + (lastBuildNumber + 1));
                    Directory.CreateDirectory(buildDir);
                }
                
                var runManager = new PipelineRunManager(buildDir);
                
                return await jobQueue.Add(runManager, pipeline.BuildJobs, CancellationToken.None);
            }

            private async Task<string> GetPipelineScript(CancellationToken cancellationToken) {
                using(await projectLock.LockAsync(cancellationToken)) {
                    return await HandleGitRepo(cancellationToken);
                }
            }

            private async Task<string> HandleGitRepo(CancellationToken cancellationToken) {
                var config = Config;
                var repoDir = Path.Combine(ProjectDir, "repo");

                if(!PathUtil.IsValidSubPath(config.Path)) {
                    throw new Exception("Invalid project path.");
                }

                if(Directory.Exists(repoDir)) {
                    Directory.Delete(repoDir, recursive: true);
                }
                await GitUtil.CloneRepo(config.Url, repoDir, config.Branch, depth: 1);

                return await File.ReadAllTextAsync(Path.Combine(repoDir, config.Path), Encoding.UTF8, cancellationToken);
            }
        }
        
    }
}