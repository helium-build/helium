using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FSharp.Control.Tasks;
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
            TransportBuildDir buildDir
        ) {
            this.buildDir = buildDir;
        }

        private readonly TransportBuildDir buildDir;
        private readonly AsyncLock stateLock = new AsyncLock();
        private AgentState state = AgentState.Initial;


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

                await buildDir.WorkspacePipe.WriteAsync(chunk.AsMemory(), cancellationToken);
            }
        }

        public async Task startBuildAsync(string task, CancellationToken cancellationToken = default(CancellationToken)) {
            using(await stateLock.LockAsync()) {
                if(state != AgentState.UploadingWorkspace) {
                    throw new InvalidState();
                }
                
                state = AgentState.RunningBuild;

                await buildDir.WorkspacePipe.CompleteAsync();
                
                try {
                    var buildTask = JsonConvert.DeserializeObject<BuildTask>(task);
                    buildDir.BuildTaskTCS.SetResult(buildTask);
                }
                catch(Exception ex) {
                    buildDir.BuildTaskTCS.TrySetException(ex);
                }
            }
        }

        public async Task<BuildStatus> getStatusAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            using(await stateLock.LockAsync()) {
                if(state != AgentState.RunningBuild) {
                    throw new InvalidState();
                }

                var stream = buildDir.BuildOutputStream;

                var buffer = new byte[4096];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                if(bytesRead == 0) {
                    stream.Close();
                    state = AgentState.BuildStopping;
                }

                return new BuildStatus {
                    Output = buffer.Take(bytesRead).ToArray(),
                };
            }
        }

        public async Task<BuildExitCode> getExitCodeAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            using(await stateLock.LockAsync()) {
                if(state != AgentState.BuildStopping) {
                    throw new InvalidState();
                }

                var result = new BuildExitCode();
                result.ExitCode = await buildDir.BuildResult;

                state = AgentState.PostBuild;
                return result;
            }
        }

        public async Task<List<string>> artifactsAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            using(await stateLock.LockAsync()) {
                if(state != AgentState.PostBuild) {
                    throw new InvalidState();
                }

                var artifactDir = buildDir.ArtifactDir;

                return Directory.EnumerateFiles(artifactDir, "*", SearchOption.AllDirectories)
                    .Select(subDir => subDir.Substring(artifactDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                    .ToList();
            }
        }

        public async Task openOutputAsync(OutputType type, string name, CancellationToken cancellationToken = default(CancellationToken)) {
            using(await stateLock.LockAsync()) {
                if(state != AgentState.PostBuild) {
                    throw new InvalidState();
                }

                if(buildDir.CurrentFileAccess != null) await buildDir.CurrentFileAccess.DisposeAsync();

                buildDir.CurrentFileAccess = null;

                if(type != OutputType.REPLAY && !PathUtil.IsValidSubPath(name)) {
                    throw new UnknownOutput();
                }

                string fileName = type switch {
                    OutputType.REPLAY => buildDir.ReplayFile,
                    OutputType.ARTIFACT => Path.Combine(buildDir.ArtifactDir, name),
                    _ => throw new UnknownOutput()
                };

                try {
                    buildDir.CurrentFileAccess = new FileStream(fileName, FileMode.Open, FileAccess.Read,
                        FileShare.Read | FileShare.Delete, 4096, true);
                }
                catch {
                    throw new UnknownOutput();
                }
            }
        }

        public async Task<byte[]> readOutputAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            using(await stateLock.LockAsync()) {
                if(state != AgentState.PostBuild) {
                    throw new InvalidState();
                }

                var stream = buildDir.CurrentFileAccess;

                if(stream == null) {
                    throw new InvalidState();
                }

                byte[] buffer = new byte[4096];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                
                if(bytesRead == 0) {
                    await stream.DisposeAsync();
                    buildDir.CurrentFileAccess = null;
                }

                return buffer.Take(bytesRead).ToArray();
            }
        }
    }
}