using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Helium.CI.Common.Protocol;
using Helium.Pipeline;
using Helium.Sdks;
using Helium.Util;
using ICSharpCode.SharpZipLib.Tar;
using Newtonsoft.Json;
using Nito.AsyncEx;

namespace Helium.CI.Agent
{
    public class BuildAgentImpl : BuildAgent.IAsync
    {
        public BuildAgentImpl(
            TransportBuildDir buildDir,
            PipeWriter workspacePipe,
            TaskCompletionSource<BuildTask> buildTaskTcs,
            Task<Stream> buildOutputStream,
            Task<int> buildResult
        ) {
            this.buildDir = buildDir;
            this.workspacePipe = workspacePipe;
            this.buildTaskTcs = buildTaskTcs;
            this.buildOutputStream = buildOutputStream;
            this.buildResult = buildResult;
        }

        private readonly TransportBuildDir buildDir;
        private readonly AsyncLock stateLock = new AsyncLock();
        private AgentState state = AgentState.Initial;

        private readonly PipeWriter workspacePipe;
        private readonly TaskCompletionSource<BuildTask> buildTaskTcs;
        private readonly Task<Stream> buildOutputStream;
        private readonly Task<int> buildResult;


        public async Task<bool> supportsPlatformAsync(string platform, CancellationToken cancellationToken = default(CancellationToken)) {
            var platformInfo = JsonConvert.DeserializeObject<PlatformInfo>(platform);
            return PlatformInfo.Current.SupportsRunning(platformInfo);
        }

        public async Task sendWorkspaceAsync(byte[] chunk, CancellationToken cancellationToken = default(CancellationToken)) {
            using(await stateLock.LockAsync()) {
                if(state == AgentState.Initial) {
                    state = AgentState.UploadingWorkspace;
                }
                
                if(state != AgentState.UploadingWorkspace) {
                    throw new InvalidState();
                }

                await workspacePipe.WriteAsync(chunk.AsMemory(), cancellationToken);
            }
        }

        public async Task startBuildAsync(string task, CancellationToken cancellationToken = default(CancellationToken)) {
            using(await stateLock.LockAsync()) {
                if(state != AgentState.UploadingWorkspace) {
                    throw new InvalidState();
                }
                
                state = AgentState.RunningBuild;
                
                try {
                    var buildTask = JsonConvert.DeserializeObject<BuildTask>(task);
                    buildTaskTcs.SetResult(buildTask);
                }
                catch(Exception ex) {
                    buildTaskTcs.TrySetException(ex);
                }
            }
        }

        public async Task<BuildStatus> getStatusAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            using(await stateLock.LockAsync()) {
                if(state != AgentState.RunningBuild) {
                    throw new InvalidState();
                }

                var stream = await buildOutputStream;

                var buffer = new byte[4096];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                if(bytesRead == 0) {
                    state = AgentState.PostBuild;
                }
                
                return new BuildStatus {
                    Output = buffer.Take(bytesRead).ToArray(), 
                };
            }
        }

        public async Task<BuildExitCode> getExitCodeAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            using(await stateLock.LockAsync()) {
                if(state != AgentState.PostBuild) {
                    throw new InvalidState();
                }

                var result = new BuildExitCode();
                result.ExitCode = await buildResult;
                return result;
            }
        }

        public Task<List<string>> artifactsAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            throw new System.NotImplementedException();
        }

        public Task openOutputAsync(OutputType type, string name, CancellationToken cancellationToken = default(CancellationToken)) {
            throw new System.NotImplementedException();
        }

        public Task<byte[]> readOutputAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            throw new System.NotImplementedException();
        }
    }
}