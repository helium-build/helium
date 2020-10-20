using System;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CI.Common;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Helium.CI.Common;
using Helium.CI.Server;
using Helium.Pipeline;
using Helium.Sdks;
using Helium.Util;
using Newtonsoft.Json;

namespace CI.Server.JobServer
{
    public class BuildServerImpl : BuildServer.BuildServerBase
    {
        public BuildServerImpl(IAgentManager agentManager, IJobQueue jobQueue) {
            this.agentManager = agentManager;
            this.jobQueue = jobQueue;
        }

        private readonly IAgentManager agentManager;
        private readonly IJobQueue jobQueue;
        
        public override async Task acceptBuildJob(IAsyncStreamReader<BuildStatusUpdate> requestStream, IServerStreamWriter<RunBuildCommand> responseStream, ServerCallContext context) {
            var agent = Authenticate(context);
            if(agent == null) return;
            
            async Task<bool> CheckPlatform(PlatformInfo platformInfo, CancellationToken cancellationToken) {
                await responseStream.WriteAsync(new RunBuildCommand {
                    SupportsPlatformRequest = new SupportsPlatformRequest {
                        Platform = JsonConvert.SerializeObject(platformInfo, typeof(PlatformInfo), new JsonSerializerSettings()),
                    },
                });

                while(await requestStream.MoveNext(cancellationToken)) {
                    var update = requestStream.Current;
                    switch(update.PayloadCase) {
                        case BuildStatusUpdate.PayloadOneofCase.PlatformSupport:
                            return update.PlatformSupport.IsSupported;
                        
                        default:
                            throw new Exception("Unexpected update type (expected PlatformSupport): " + update.PayloadCase);
                    }
                }
                
                throw new Exception("Unexpected end of stream (expected PlatformSupport)");
            }

            IRunnableJob? runnableJob = null;
            try {
                runnableJob = await jobQueue.AcceptJob(CheckPlatform, context.CancellationToken);
                
                var cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, runnableJob.JobStatus.BuildCancelToken).Token;

                runnableJob.JobStatus.Started(agent.Config);
                
                var task = JsonConvert.SerializeObject(runnableJob.BuildTask, typeof(BuildTaskBase), new JsonSerializerSettings());

                await responseStream.WriteAsync(new RunBuildCommand {
                    BuildTask = task,
                });


                await SendWorkspace(responseStream, runnableJob, cancellationToken);


                var result = await ProcessBuildOutput(requestStream, cancellationToken, runnableJob);

                if(result.ExitCode != 0) {
                    await runnableJob.JobStatus.FailedWith(result.ExitCode);
                    return;
                }

                if(runnableJob.BuildTask.ReplayMode != ReplayMode.Discard) {
                    await ReadReplay(requestStream, responseStream, runnableJob.JobStatus, cancellationToken);
                }

                foreach(var artifact in result.Artifacts) {
                    await ReadArtifact(requestStream, responseStream, runnableJob.JobStatus, artifact, cancellationToken);
                }

                await runnableJob.JobStatus.Completed();
            }
            catch(OperationCanceledException) {
                throw;
            }
            catch(Exception ex) {
                if(runnableJob != null) {
                    await runnableJob.JobStatus.Error(ex);
                }
                else {
                    throw;
                }
            }
            

        }

        private IAgent? Authenticate(ServerCallContext context) {
            var header = context.RequestHeaders.FirstOrDefault(entry => ProtocolConstants.AgentKeyHeaderName.Equals(entry.Key, StringComparison.OrdinalIgnoreCase));
            if(header == null) return null;

            return agentManager.Authenticate(header.Value);
        }

        private static async Task<JobResult> ProcessBuildOutput(IAsyncStreamReader<BuildStatusUpdate> requestStream, CancellationToken cancellationToken, IRunnableJob runnableJob) {
            while(await requestStream.MoveNext(cancellationToken)) {
                var update = requestStream.Current;
                switch(update.PayloadCase) {
                    case BuildStatusUpdate.PayloadOneofCase.BuildOutput:
                        await runnableJob.JobStatus.AppendOutput(update.BuildOutput.ToByteArray(), cancellationToken);
                        break;

                    case BuildStatusUpdate.PayloadOneofCase.JobFinished:
                        return update.JobFinished;

                    default:
                        throw new Exception("Unexpected update type (expected BuildOutput or JobFinished): " + update.PayloadCase);
                }
            }
            
            throw new Exception("Unexpected end of stream (expected BuildOutput or JobFinished)");
        }

        private static async Task SendWorkspace(IServerStreamWriter<RunBuildCommand> responseStream, IRunnableJob runnableJob, CancellationToken cancellationToken) {
            var workspacePipe = new Pipe();

            var workspaceTask = Task.Run(async () => {
                await using var stream = workspacePipe.Writer.AsStream();
                await runnableJob.WriteWorkspace(stream, cancellationToken);
            });

            await using(var stream = workspacePipe.Reader.AsStream()) {
                var buffer = new byte[4096];
                int bytesRead;
                while((bytesRead = await stream.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0) {
                    await responseStream.WriteAsync(new RunBuildCommand {
                        WorkspaceContent = ByteString.CopyFrom(buffer, 0, bytesRead),
                    });
                }
            }

            await workspaceTask;

            await responseStream.WriteAsync(new RunBuildCommand {
                WorkspaceEnd = new Empty(),
            });
        }
        
        private async Task ReadReplay(IAsyncStreamReader<BuildStatusUpdate> requestStream, IServerStreamWriter<RunBuildCommand> responseStream, IJobStatusUpdatable jobStatus, CancellationToken cancellationToken) {
            await responseStream.WriteAsync(new RunBuildCommand {
                Artifact = new ArtifactRequest {
                    Replay = new Empty(),
                },
            });
            
            await using var fileStream = jobStatus.OpenReplay();
            await ReadArtifactContent(requestStream, fileStream, cancellationToken);
        }

        private async Task ReadArtifact(IAsyncStreamReader<BuildStatusUpdate> requestStream, IServerStreamWriter<RunBuildCommand> responseStream, IJobStatusUpdatable jobStatus, ArtifactInfo artifact, CancellationToken cancellationToken) {
            var artifactName = artifact.Name;
                
            if(artifactName == null || !PathUtil.IsValidSubPath(artifactName) || string.IsNullOrEmpty(artifactName)) {
                throw new Exception("Invalid path name.");
            }

            await responseStream.WriteAsync(new RunBuildCommand {
                Artifact = new ArtifactRequest {
                    Artifact = artifact,
                },
            });
            
            var path = Path.Combine(jobStatus.ArtifactDir, artifactName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            await using var fileStream = File.Create(path);
            await ReadArtifactContent(requestStream, fileStream, cancellationToken);
        }

        private async Task ReadArtifactContent(IAsyncStreamReader<BuildStatusUpdate> requestStream, Stream fileStream, CancellationToken cancellationToken) {
            while(await requestStream.MoveNext()) {
                var update = requestStream.Current;
                switch(update.PayloadCase) {
                    case BuildStatusUpdate.PayloadOneofCase.ArtifactData:
                        await fileStream.WriteAsync(update.ArtifactData.ToByteArray().AsMemory(), cancellationToken);
                        break;
                    
                    case BuildStatusUpdate.PayloadOneofCase.ArtifactEnd:
                        return;
                    
                    default:
                        throw new Exception("Unexpected update type (expected ArtifactData or ArtifactEnd): " + update.PayloadCase);
                }
            }
            
            throw new Exception("Unexpected end of stream (expected ArtifactData or ArtifactEnd)");
        }

    }
}