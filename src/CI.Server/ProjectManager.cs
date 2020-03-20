using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Helium.Env;
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
            private readonly Dictionary<int, IPipelineStatus> statuses = new Dictionary<int, IPipelineStatus>();
            
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

            public async Task<IPipelineStatus?> GetPipelineStatus(int buildNum) {
                if(buildNum < 1) return null;
                
                using(await projectLock.LockAsync()) {
                    if(statuses.TryGetValue(buildNum, out var status)) return status;

                    var dir = Path.Combine(ProjectDir, "builds", "build" + buildNum);
                    if(!Directory.Exists(dir)) return null;
                    
                    status = await CompletedPipelineStatus.Load(dir, buildNum);
                    statuses[buildNum] = status;
                    return status;
                }
            }

            public async Task<IPipelineStatus> StartBuild(PipelineInfo pipeline) {
                using(await projectLock.LockAsync()) {
                    var buildsDir = Path.Combine(ProjectDir, "builds");
                    Directory.CreateDirectory(buildsDir);
                    int lastBuildNumber = Directory.GetDirectories(buildsDir, "build*")
                        .Select(dir =>
                            int.TryParse(Path.GetFileName(dir).Substring(5), out var num) ? (int?) num : null)
                        .DefaultIfEmpty(null)
                        .Max()
                        ?? 0;

                    int buildNum = lastBuildNumber + 1;

                    var buildDir = Path.Combine(buildsDir, "build" + buildNum);
                    Directory.CreateDirectory(buildDir);
                    
                    var runManager = new PipelineRunManager(buildDir);
                
                    var status = await jobQueue.Add(runManager, pipeline.BuildJobs, buildNum, CancellationToken.None);
                    statuses[buildNum] = status;
                    return status;
                }
                
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

    internal class CompletedPipelineStatus : IPipelineStatus
    {
        private CompletedPipelineStatus(string dir, int buildNumber, IReadOnlyDictionary<string, IJobStatus> jobsStatus, BuildState state) {
            this.dir = dir;
            BuildNumber = buildNumber;
            JobsStatus = jobsStatus;
            State = state;
        }
        
        private readonly string dir;

        public int BuildNumber { get; }
        public IReadOnlyDictionary<string, IJobStatus> JobsStatus { get; }
        public BuildState State { get; }

        public event EventHandler PipelineCompleted {
            add {}
            remove {}
        }
        
        public async Task<GrowList<string>> OutputLines() {
            var lines = await File.ReadAllLinesAsync(Path.Combine(dir, "output.log"));
            return new GrowList<string>(lines);
        }

        public event EventHandler<OutputLinesChangedEventArgs> OutputLinesChanged {
            add {}
            remove { }
        }

        public static async Task<IPipelineStatus> Load(string dir, int buildNumber) {
            var jobsDir = Path.Combine(dir, "jobs");
            var jobs = await Directory.EnumerateFiles(jobsDir)
                .ToAsyncEnumerable()
                .SelectAwait(async jobDir => {
                    var jobId = jobDir
                        .Substring(jobsDir.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    return await CompletedJobStatus.Load(jobDir, jobId);
                })
                .ToDictionaryAsync(jobStatus => jobStatus.Job.Id);

            BuildState state = BuildState.Failed;
            try {
                var result = JsonConvert.DeserializeObject<PipelineBuildResult>(
                    await File.ReadAllTextAsync(Path.Combine(dir, "result.json"))
                );

                state = result.State;
            }
            catch(IOException) {}
            
            return new CompletedPipelineStatus(dir, buildNumber, jobs, state);
        }
    }

    internal static class CompletedJobStatus
    {
        public static async Task<IJobStatus> Load(string jobDir, string jobId) {
            return null!;
        }
    }
}