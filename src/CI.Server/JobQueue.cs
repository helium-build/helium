using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Helium.CI.Common.Protocol;
using Helium.Pipeline;
using Helium.Util;
using ICSharpCode.SharpZipLib.Tar;
using Jint.Native.Object;
using LibGit2Sharp;
using Newtonsoft.Json;
using Nito.AsyncEx;

namespace Helium.CI.Server
{
    using BuildInputHandler = Func<CancellationToken, Task<string>>;
    
    internal class JobQueue : IJobQueue
    {
        private readonly AsyncMonitor monitor = new AsyncMonitor();
        private readonly LinkedList<RunnableJob> jobQueue = new LinkedList<RunnableJob>();
        
        public async Task<Dictionary<BuildJob, IJobStatus>> Add(IPipelineManager pipelineManager, IEnumerable<BuildJob> jobs, CancellationToken cancellationToken) {
            var dependentJobs = new HashSet<BuildJob>();
            var jobMap = new Dictionary<BuildJob, IJobStatus>();

            var runnableJobs = new List<RunnableJob>();
            
            foreach(var job in jobs) {
                AddBuildJob(pipelineManager, job, runnableJobs, dependentJobs, jobMap);
            }
            
            runnableJobs.Reverse();
            
            using(await monitor.EnterAsync(cancellationToken)) {
                foreach(var jobRun in runnableJobs) {
                    jobQueue.AddLast(jobRun);
                }
                
                monitor.PulseAll();
            }

            return jobMap;
        }

        private void AddBuildJob(IPipelineManager pipelineManager, BuildJob job, List<RunnableJob> runnableJobs, HashSet<BuildJob> dependentJobs, IDictionary<BuildJob, IJobStatus> jobMap) {
            if(dependentJobs.Contains(job)) throw new CircularDependencyException();

            if(jobMap.ContainsKey(job)) return;

            dependentJobs.Add(job);

            foreach(var input in job.Input) {
                switch(input.Source) {
                    case ArtifactBuildInput artifact:
                        AddBuildJob(pipelineManager, job, runnableJobs, dependentJobs, jobMap);
                        break;
                }
            }

            dependentJobs.Remove(job);

            var runnable = new RunnableJob(pipelineManager, job, jobMap);
            jobMap.Add(job, runnable.Status);
            runnableJobs.Add(runnable);
        }

        private class RunnableJob : IRunnableJob
        {
            public RunnableJob(IPipelineManager pipelineManager, BuildJob job, IDictionary<BuildJob, IJobStatus> jobMap) {
                this.pipelineManager = pipelineManager;
                BuildTask = job.Task;
                Status = new JobStatus(pipelineManager.NextArtifactDir());
                
                var inputHandlers = new List<(BuildInputHandler handler, string path)>();
                foreach(var input in job.Input) {
                    var handler = input.Source switch {
                        GitBuildInput git => HandleGitInput(git),
                        HttpRequestBuildInput http => HandleHttpInput(http),
                        ArtifactBuildInput artifact => HandleArtifactInput(artifact, jobMap[artifact.Job]),
                        _ => throw new Exception("Unknown build input type")
                    };
                    
                    inputHandlers.Add((handler, input.Path));
                }
                this.inputHandlers = inputHandlers;
            }
            
            private readonly IPipelineManager pipelineManager;
            private readonly IReadOnlyList<(BuildInputHandler handler, string path)> inputHandlers;
            private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();


            public BuildTask BuildTask { get; }
            public JobStatus Status { get; }

            public async Task Run(BuildAgent.IAsync agent, CancellationToken cancellationToken) {
                try {
                    var combinedToken = CancellationTokenSource
                        .CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token).Token;

                    await WriteWorkspace(agent, combinedToken);
                    await agent.startBuildAsync(JsonConvert.ToString(BuildTask), combinedToken);

                    await ReadConsole(agent, combinedToken);

                    int exitCode = await agent.getExitCodeAsync(combinedToken);
                    if(exitCode != 0) {
                        Status.FailedWith(exitCode);
                        return;
                    }

                    foreach(var artifact in await agent.artifactsAsync(combinedToken)) {
                        await ReadArtifact(agent, artifact, combinedToken);
                    }

                    Status.Completed();
                }
                catch(Exception ex) {
                    Status.Error(ex);
                    throw;
                }
            }

            private async Task ReadArtifact(BuildAgent.IAsync agent, string artifact, CancellationToken cancellationToken) {
                if(!PathUtil.IsValidSubPath(artifact) || string.IsNullOrEmpty(artifact)) {
                    throw new Exception("Invalid path name.");
                }

                await agent.openOutputAsync(OutputType.ARTIFACT, artifact, cancellationToken);

                var path = Path.Combine(Status.ArtifactDir, artifact);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                await using var fileStream = File.Create(artifact);
                while(true) {
                    var data = await agent.readOutputAsync(cancellationToken);
                    if(data.Length == 0) break;
                    await fileStream.WriteAsync(data, cancellationToken);
                }
            }

            private async Task ReadConsole(BuildAgent.IAsync agent, CancellationToken cancellationToken) {
                while(true) {
                    var status = await agent.getStatusAsync(cancellationToken);
                    if(string.IsNullOrEmpty(status.Output)) {
                        break;
                    }

                    await Status.AppendOutput(status.Output, cancellationToken);
                }
            }

            private async Task WriteWorkspace(BuildAgent.IAsync agent, CancellationToken cancellationToken) {
                await using var workspaceStream = new WorkspaceStream(agent);
                await using var tarStream = new TarOutputStream(workspaceStream);
                
                foreach(var inputHandler in inputHandlers) {
                    var file = await inputHandler.handler(cancellationToken);
                    await ArchiveUtil.AddFileOrDirToTar(tarStream, inputHandler.path, file);
                }
            }

            private BuildInputHandler HandleGitInput(GitBuildInput git) => async cancellationToken => {
                var dir = pipelineManager.NextInputPath();

                Directory.CreateDirectory(dir);

                Repository.Clone(git.Url, dir, new CloneOptions {
                    BranchName = git.Ref,
                    RecurseSubmodules = true,
                });

                return dir;
            };

            private BuildInputHandler HandleHttpInput(HttpRequestBuildInput http) => async cancellationToken => {
                var tempFile = pipelineManager.NextInputPath();
                
                await HttpUtil.FetchFileValidate(http.Url, tempFile, http.Hash.Validate);
                return tempFile;
            };

            private BuildInputHandler HandleArtifactInput(ArtifactBuildInput artifact, IJobStatus jobStatus) => async cancellationToken => {
                if(!PathUtil.IsValidSubPath(artifact.ArtifactPath)) {
                    throw new Exception("Invalid artifact path");
                }

                await jobStatus.WaitForComplete(cancellationToken);

                return Path.Combine(Status.ArtifactDir, artifact.ArtifactPath);
            };

            public void CancelBuild() {
                cancellationTokenSource.Cancel();
            }
        }

        public async Task<IRunnableJob> AcceptJob(Func<BuildTask, Task<bool>> jobFilter, CancellationToken cancellationToken) {
            using(await monitor.EnterAsync(cancellationToken)) {
                while(true) {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var job = await jobQueue.ToAsyncEnumerable().FirstOrDefaultAwaitAsync(async j => await jobFilter(j.BuildTask), cancellationToken);
                    if(job != null) {
                        return job;
                    }

                    await monitor.WaitAsync(cancellationToken);
                }
            }
        }
    }

    internal class WorkspaceStream : Stream
    {
        public WorkspaceStream(BuildAgent.IAsync agent) {
            this.agent = agent;
        }
        
        private readonly BuildAgent.IAsync agent;

        public override void Write(byte[] buffer, int offset, int count) {
            WriteAsync(buffer, offset, count, CancellationToken.None).Wait();
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            if(offset == 0 && count == buffer.Length) {
                await agent.sendWorkspaceAsync(buffer, cancellationToken);
            }
            else {
                var b = new byte[count];
                Buffer.BlockCopy(buffer, offset, b, 0, count);
                await agent.sendWorkspaceAsync(b, cancellationToken);
            }
        }

        public override int Read(byte[] buffer, int offset, int count) {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotSupportedException();
        }

        public override void SetLength(long value) {
            throw new NotSupportedException();
        }

        public override void Flush() {}
        public override async Task FlushAsync(CancellationToken cancellationToken) {}

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
    }
}