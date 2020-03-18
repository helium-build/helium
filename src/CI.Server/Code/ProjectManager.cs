using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Helium.Util;
using LibGit2Sharp;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Nito.AsyncEx;

namespace Helium.CI.Server
{
    public sealed class ProjectManager : IProjectManager
    {
        private ProjectManager(string projectsDir, IJobQueue jobQueue, IDictionary<string, IProject> projects, CancellationToken cancellationToken) {
            this.projectsDir = projectsDir;
            this.jobQueue = jobQueue;
            this.projects = new ConcurrentDictionary<string, IProject>(projects);
            this.cancellationToken = cancellationToken;
        }

        private readonly string projectsDir;
        private readonly IJobQueue jobQueue;
        private readonly ConcurrentDictionary<string, IProject> projects;
        private readonly CancellationToken cancellationToken;

        public Task<IProject?> GetProject(string id) {
            throw new System.NotImplementedException();
        }

        public IReadOnlyCollection<IProject> Projects => new ReadOnlyCollectionNoList<IProject>(projects.Values);

        public async Task<IProject> AddProject(string id, ProjectConfig config) {
            var dir = Path.Combine(projectsDir, id);
            var project = new Project(dir, id, jobQueue, config);

            await project.WriteConfig(null, cancellationToken);

            return project;
        }

        public Task RemoveProject(IProject project) {
            throw new System.NotImplementedException();
        }

        public static async Task<IProjectManager> Load(string projectsDir, IJobQueue jobQueue, CancellationToken cancellationToken) {
            var projects = new Dictionary<string, IProject>();

            Directory.CreateDirectory(projectsDir);

            foreach(var projectDir in Directory.EnumerateDirectories(projectsDir)) {
                cancellationToken.ThrowIfCancellationRequested();

                var id = Path.GetFileName(projectDir);
                
                var configStr = await File.ReadAllTextAsync(Path.Combine(projectDir, "project.json"), cancellationToken);
                var config = JsonConvert.DeserializeObject<ProjectConfig>(configStr);
                
                var project = new Project(Path.GetFullPath(projectDir), id, jobQueue, config);
                projects[id] = project;
            }
            
            return new ProjectManager(projectsDir, jobQueue, projects, cancellationToken);
        }

        private sealed class Project : IProject
        {
            public Project(string dir, string id, IJobQueue jobQueue, ProjectConfig config) {
                this.dir = dir;
                this.id = id;
                this.jobQueue = jobQueue;
                Config = config;
            }

            private readonly AsyncLock projectLock = new AsyncLock();
            private readonly string dir;
            private readonly string id;
            private readonly IJobQueue jobQueue;
            
            public ProjectConfig Config { get; private set; }

            public async Task WriteConfig(ProjectConfig? config, CancellationToken cancellationToken) {
                if(config == null) config = Config;
                var tmpFile = Path.Combine(dir, "project.json.tmp");
                var configStr = JsonConvert.SerializeObject(config);
                
                Directory.CreateDirectory(dir);
                
                await FileUtil.WriteAllTextToDiskAsync(tmpFile, configStr, Encoding.UTF8, cancellationToken);
                File.Move(tmpFile, Path.Combine(dir, "project.json"), true);
            }
            
            public async Task UpdateConfig(ProjectConfig config, CancellationToken cancellationToken) {
                await WriteConfig(config, cancellationToken);
                using(await projectLock.LockAsync(cancellationToken)) {
                    Config = config;
                }
            }

            public async Task<string> GetPipelineScript(CancellationToken cancellationToken) {
                using(await projectLock.LockAsync(cancellationToken)) {
                    return HandleGitRepo();
                }
            }

            private string HandleGitRepo() {
                var config = Config;
                var repoDir = Path.Combine(dir, "repo");

                bool requiresPull = true;
                
                void DeleteRepo() {
                    Directory.Delete(repoDir, recursive: true);
                }

                void CloneRepo() {
                    requiresPull = false;
                    Repository.Clone(config.Url, repoDir, new CloneOptions {
                        BranchName = config.Branch,
                        IsBare = true,
                    });
                }

                if(!Directory.Exists(repoDir)) {
                    CloneRepo();
                }

                

                using var repo = new Repository(repoDir);
                Remote? remote = null;
                try {
                    remote = repo.Network.Remotes.First(r => r.Url == config.Url);
                }
                catch {
                }

                if(remote == null) {
                    DeleteRepo();
                    CloneRepo();
                }

                if(requiresPull) {
                    Commands.Pull(repo, null, new PullOptions {
                        MergeOptions = new MergeOptions {
                            FastForwardStrategy = FastForwardStrategy.FastForwardOnly,
                        }
                    });
                }

                var blob = (Blob)repo.Branches[config.Branch].Tip[config.Path].Target;
                return blob.GetContentText(Encoding.UTF8);
            }
        }
        
    }
}