using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CI.Common;
using Grpc.Core;
using Helium.CI.Common;
using Helium.Pipeline;
using Helium.Sdks;
using Helium.Util;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Nito.AsyncEx;

namespace Helium.CI.Agent
{
    public class BuildAgent
    {
        public BuildAgent(ILogger logger, BuildServer.BuildServerClient buildServer, string apiKey, string workspacesDir, int maxJobs) {
            this.logger = logger;
            this.buildServer = buildServer;
            this.apiKey = apiKey;
            this.workspacesDir = workspacesDir;
            this.maxJobs = maxJobs;
        }
        
        private readonly ILogger logger;
        private readonly BuildServer.BuildServerClient buildServer;
        private readonly string apiKey;
        private readonly string workspacesDir;
        private readonly AsyncMonitor jobLock = new AsyncMonitor();
        private readonly int maxJobs;
        private int runningJobs = 0;
        
        private readonly PlatformInfo currentPlatform = PlatformInfo.Current;

        public async Task JobLoop(CancellationToken cancellationToken) {
            try {
                using(await jobLock.EnterAsync(cancellationToken)) {
                    while(!cancellationToken.IsCancellationRequested) {
                        while(runningJobs < maxJobs) {
                            _ = Task.Run(() => JobFiber(cancellationToken), cancellationToken);
                            ++runningJobs;
                        }

                        await jobLock.WaitAsync(cancellationToken);
                    }
                }
            }
            catch(OperationCanceledException) {}
            
            
        }

        private async Task JobFiber(CancellationToken cancellationToken) {
            try {
                var headers = new Metadata {
                    { ProtocolConstants.AgentKeyHeaderName, apiKey }
                };
                using var stream = buildServer.acceptBuildJob(headers, null, cancellationToken);


                var buildTask = await ReadBuildTask(stream, cancellationToken);
                if(buildTask == null) {
                    return;
                }
                
                
                await using var buildDir = DirectoryCleanup.CreateTempDir(workspacesDir);
                logger.LogInformation("Created workspace for build: {0}, exists: {1}", buildDir.Value, Directory.Exists(buildDir.Value));
                var buildRunner = new BuildJobRunner(logger, buildDir.Value, stream.ResponseStream, stream.RequestStream, cancellationToken);
                await buildRunner.RunJob(buildTask);

                await ProcessArtifacts(stream, buildRunner, cancellationToken);
            }
            catch(OperationCanceledException) { }
            catch(Exception ex) {
                logger.LogError(ex, "Error running build job");
                await Task.Delay(1000, cancellationToken);
            }
            finally {
                using(await jobLock.EnterAsync(cancellationToken)) {
                    --runningJobs;
                    jobLock.Pulse();
                }
            }
        }

        private async Task<BuildTaskBase?> ReadBuildTask(AsyncDuplexStreamingCall<BuildStatusUpdate, RunBuildCommand> stream, CancellationToken cancellationToken) {
            while(await stream.ResponseStream.MoveNext(cancellationToken)) {
                var command = stream.ResponseStream.Current;
                switch(command.PayloadCase) {
                    case RunBuildCommand.PayloadOneofCase.SupportsPlatformRequest:
                        await stream.RequestStream.WriteAsync(new BuildStatusUpdate {
                            PlatformSupport = await SupportsPlatform(command.SupportsPlatformRequest)
                        });
                        break;

                    case RunBuildCommand.PayloadOneofCase.BuildTask:
                        return JsonConvert.DeserializeObject<BuildTaskBase>(command.BuildTask);

                    default:
                        throw new Exception("Unexpected command type (expected SupportsPlatformRequest or BuildTask): " + command.PayloadCase);
                }
            }

            return null;
        }


        private async Task<SupportsPlatformResponse> SupportsPlatform(SupportsPlatformRequest request) {
            var platform = request.Platform;
            if(platform == null) throw new ArgumentException("Platform must be specified");
            
            logger.LogTrace($"Checking platform support: {platform}");
            
            var platformInfo = JsonConvert.DeserializeObject<PlatformInfo>(platform);
            return new SupportsPlatformResponse {
                IsSupported = currentPlatform.SupportsRunning(platformInfo),
            };
        }

        private async Task ProcessArtifacts(AsyncDuplexStreamingCall<BuildStatusUpdate, RunBuildCommand> stream, BuildJobRunner buildRunner, CancellationToken cancellationToken) {
            while(await stream.ResponseStream.MoveNext(cancellationToken)) {
                var command = stream.ResponseStream.Current;
                switch(command.PayloadCase) {
                    case RunBuildCommand.PayloadOneofCase.Artifact:
                        var artifact = command.Artifact;
                        switch(artifact.PayloadCase) {
                            case ArtifactRequest.PayloadOneofCase.Replay:
                                await buildRunner.SendReplay();
                                break;
                            
                            case ArtifactRequest.PayloadOneofCase.Artifact:
                                await buildRunner.SendArtifact(artifact.Artifact);
                                break;
                            
                            default:
                                throw new Exception("Unknown artifact type.");
                        }
                        break;
                        
                    default:
                        throw new Exception("Unexpected build request command");
                }
            }
        }
    }
}