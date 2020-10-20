using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Helium.CI.Common;
using Helium.Pipeline;
using Helium.Sdks;
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
        
        public async Task<IPipelineStatus> AddJobs(IPipelineRunManager pipelineRunManager, IEnumerable<BuildJob> jobs, int buildNum, CancellationToken cancellationToken) {
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
                    await jobRun.Status.WriteBuildJobFile();
                    jobQueue.AddLast(jobRun);
                }
                
                monitor.PulseAll();
            }

            return new PipelineStatus(
                new ReadOnlyDictionary<string, IJobStatus>(jobMap.ToDictionary(kvp => kvp.Key.Id, kvp => kvp.Value)),
                buildNum,
                pipelineRunManager.PipelineDir
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
            jobMap.Add(job, runnable.JobStatus);
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


            public BuildTaskBase BuildTask { get; }
            public IJobStatusUpdatable JobStatus => Status;
            
            public JobStatus Status { get; }

            
            public async Task WriteWorkspace(Stream stream, CancellationToken cancellationToken) {
                await using var tarStream = new TarOutputStream(stream);
                
                foreach(var inputHandler in inputHandlers) {
                    cancellationToken.ThrowIfCancellationRequested();
                    
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
        }

        public async Task<IRunnableJob> AcceptJob(PlatformChecker platformChecker, CancellationToken cancellationToken) {
            using(await monitor.EnterAsync(cancellationToken)) {
                while(true) {
                    cancellationToken.ThrowIfCancellationRequested();

                    for(var node = jobQueue.First; node != null; node = node.Next) {
                        cancellationToken.ThrowIfCancellationRequested();
                        if(await platformChecker(node.Value.BuildTask.Platform, cancellationToken)) {
                            jobQueue.Remove(node);
                            return node.Value;
                        }
                    }
                    
                    await monitor.WaitAsync(cancellationToken);
                }
            }
        }
    }
}