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
using Newtonsoft.Json;
using Nito.AsyncEx;

namespace Helium.CI.Server
{
    using BuildInputHandler = Func<CancellationToken, Task<string>>;
    
    public class JobQueue : IJobQueue
    {
        private readonly AsyncMonitor monitor = new AsyncMonitor();
        private readonly LinkedList<RunnableJob> jobQueue = new LinkedList<RunnableJob>();
        
        public async Task<IPipelineStatus> Add(IPipelineRunManager pipelineRunManager, IEnumerable<BuildJob> jobs, int buildNum, CancellationToken cancellationToken) {
            var dependentJobs = new HashSet<BuildJob>();
            var jobMap = new Dictionary<BuildJob, IJobStatus>();

            var runnableJobs = new List<RunnableJob>();
            
            foreach(var job in jobs) {
                AddBuildJob(pipelineRunManager, job, runnableJobs, dependentJobs, jobMap);
            }
            
            var jobIds = new HashSet<string>(jobMap.Count);
            foreach(var buildJob in jobMap.Keys) {
                if(!jobIds.Add(buildJob.Id)) {
                    throw new Exception("Duplicate job id.");
                }
            }
            
            runnableJobs.Reverse();
            
            using(await monitor.EnterAsync(cancellationToken)) {
                foreach(var jobRun in runnableJobs) {
                    jobQueue.AddLast(jobRun);
                }
                
                monitor.PulseAll();
            }

            return new PipelineStatus(
                jobMap.ToDictionary(kvp => kvp.Key.Id, kvp => kvp.Value),
                buildNum
            ); 
        }

        private void AddBuildJob(IPipelineRunManager pipelineRunManager, BuildJob job, List<RunnableJob> runnableJobs, HashSet<BuildJob> dependentJobs, IDictionary<BuildJob, IJobStatus> jobMap) {
            if(dependentJobs.Contains(job)) throw new CircularDependencyException();

            if(jobMap.ContainsKey(job)) return;

            dependentJobs.Add(job);

            foreach(var input in job.Input) {
                switch(input.Source) {
                    case ArtifactBuildInput artifact:
                        AddBuildJob(pipelineRunManager, artifact.Job, runnableJobs, dependentJobs, jobMap);
                        break;
                }
            }

            dependentJobs.Remove(job);

            var runnable = new RunnableJob(pipelineRunManager, job, jobMap);
            jobMap.Add(job, runnable.Status);
            runnableJobs.Add(runnable);
        }

        private class RunnableJob : IRunnableJob
        {
            public RunnableJob(IPipelineRunManager pipelineRunManager, BuildJob job, IDictionary<BuildJob, IJobStatus> jobMap) {
                BuildTask = job.Task;
                Status = new JobStatus(pipelineRunManager.BuildPath(job), job);
                
                var inputHandlers = new List<(BuildInputHandler handler, string path)>();
                foreach(var input in job.Input) {
                    var handler = input.Source switch {
                        GitBuildInput git => HandleGitInput(pipelineRunManager, git),
                        HttpRequestBuildInput http => HandleHttpInput(pipelineRunManager, http),
                        ArtifactBuildInput artifact => HandleArtifactInput(artifact, jobMap[artifact.Job]),
                        _ => throw new Exception("Unknown build input type")
                    };
                    
                    inputHandlers.Add((handler, input.Path));
                }
                this.inputHandlers = inputHandlers;
            }
            
            private readonly IReadOnlyList<(BuildInputHandler handler, string path)> inputHandlers;
            private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();


            public BuildTask BuildTask { get; }
            public JobStatus Status { get; }

            public async Task Run(BuildAgent.IAsync agent, AgentConfig agentConfig, CancellationToken cancellationToken) {
                try {
                    var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token).Token;

                    await WriteWorkspace(agent, combinedToken);
                    await agent.startBuildAsync(JsonConvert.SerializeObject(BuildTask), combinedToken);
                    Status.Started(agentConfig);

                    await ReadConsole(agent, combinedToken);

                    int exitCode = (await agent.getExitCodeAsync(combinedToken)).ExitCode;
                    if(exitCode != 0) {
                        await Status.FailedWith(exitCode);
                        return;
                    }

                    await ReadReplay(agent, combinedToken);

                    foreach(var artifact in await agent.artifactsAsync(combinedToken)) {
                        await ReadArtifact(agent, artifact, combinedToken);
                    }

                    await Status.Completed();
                }
                catch(Exception ex) {
                    await Status.Error(ex);
                    throw;
                }
            }

            private async Task ReadReplay(BuildAgent.IAsync agent, CancellationToken combinedToken) {
                await agent.openOutputAsync(OutputType.REPLAY, "", combinedToken);

                await using var fileStream = Status.OpenReplay();
                await ReadOutput(agent, fileStream, combinedToken);
            }

            private async Task ReadArtifact(BuildAgent.IAsync agent, string artifact, CancellationToken cancellationToken) {
                if(!PathUtil.IsValidSubPath(artifact) || string.IsNullOrEmpty(artifact)) {
                    throw new Exception("Invalid path name.");
                }

                await agent.openOutputAsync(OutputType.ARTIFACT, artifact, cancellationToken);
                
                var path = Path.Combine(Status.ArtifactDir, artifact);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                await using var fileStream = File.Create(path);

                await ReadOutput(agent, fileStream, cancellationToken);
            }

            private async Task ReadOutput(BuildAgent.IAsync agent, Stream fileStream, CancellationToken cancellationToken) {
                while(true) {
                    var data = await agent.readOutputAsync(cancellationToken);
                    if(data.Length == 0) break;
                    await fileStream.WriteAsync(data, cancellationToken);
                }
            }

            private async Task ReadConsole(BuildAgent.IAsync agent, CancellationToken cancellationToken) {
                while(true) {
                    var status = await agent.getStatusAsync(cancellationToken);
                    if(status.Output == null || status.Output.Length == 0) {
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

            private static BuildInputHandler HandleGitInput(IPipelineRunManager pipelineRunManager, GitBuildInput git) => async cancellationToken => {
                var dir = pipelineRunManager.NextInputPath();

                Directory.CreateDirectory(dir);

                await GitUtil.CloneRepo(git.Url, dir, git.Branch);

                return dir;
            };

            private static BuildInputHandler HandleHttpInput(IPipelineRunManager pipelineRunManager, HttpRequestBuildInput http) => async cancellationToken => {
                var tempFile = pipelineRunManager.NextInputPath();
                
                await HttpUtil.FetchFileValidate(http.Url, tempFile, http.Hash.Validate);
                return tempFile;
            };

            private static BuildInputHandler HandleArtifactInput(ArtifactBuildInput artifact, IJobStatus jobStatus) => async cancellationToken => {
                if(!PathUtil.IsValidSubPath(artifact.ArtifactPath)) {
                    throw new Exception("Invalid artifact path");
                }

                await jobStatus.WaitForComplete(cancellationToken);

                return Path.Combine(jobStatus.ArtifactDir, artifact.ArtifactPath);
            };

            public void CancelBuild() {
                cancellationTokenSource.Cancel();
            }
        }

        public async Task<IRunnableJob> AcceptJob(Func<BuildTask, Task<bool>> jobFilter, CancellationToken cancellationToken) {
            using(await monitor.EnterAsync(cancellationToken)) {
                while(true) {
                    cancellationToken.ThrowIfCancellationRequested();

                    for(var node = jobQueue.First; node != null; node = node.Next) {
                        cancellationToken.ThrowIfCancellationRequested();
                        if(await jobFilter(node.Value.BuildTask)) {
                            jobQueue.Remove(node);
                            return node.Value;
                        }
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