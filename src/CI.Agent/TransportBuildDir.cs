using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Helium.CI.Common.Protocol;
using Helium.Env;
using Helium.Pipeline;
using Helium.Util;
using ICSharpCode.SharpZipLib.Tar;
using Nito.AsyncEx;
using Thrift.Transport;

namespace Helium.CI.Agent
{
    public class TransportBuildDir : TTransport
    {
        public TransportBuildDir(DirectoryCleanup<string> buildDir, TTransport transport, CancellationToken cancellationToken) {
            this.buildDir = buildDir;
            this.transport = transport;
            buildOutputStream = buildOutputPipe.Reader.AsStream();
            var combined = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, buildCancel.Token);
            buildRunTask = Task.Run(() => RunBuild(combined.Token));
        }

        private readonly AsyncLock workspaceLock = new AsyncLock();
        private readonly CancellationTokenSource buildCancel = new CancellationTokenSource(); 
        private readonly DirectoryCleanup<string> buildDir;
        private readonly TTransport transport;
        
        private readonly Pipe workspacePipe = new Pipe();
        private readonly TaskCompletionSource<BuildTaskBase> buildStartTask = new TaskCompletionSource<BuildTaskBase>();
        private readonly Pipe buildOutputPipe = new Pipe();
        private readonly Stream buildOutputStream;
        private readonly Task<int> buildRunTask;
        
        

        public PipeWriter WorkspacePipe => workspacePipe.Writer;
        public TaskCompletionSource<BuildTaskBase> BuildTaskTCS => buildStartTask;
        public Stream BuildOutputStream => buildOutputStream;
        public Task<int> BuildResult => buildRunTask;
        public string ArtifactDir => Path.Combine(buildDir.Value, "artifacts");
        public string ReplayFile => Path.Combine(buildDir.Value, "replay.tar");

        public FileStream? CurrentFileAccess { get; set; }

        
        
        public override bool IsOpen => transport.IsOpen;

        private string WorkspaceDir => Path.Combine(buildDir.Value, "workspace");
        

        private async Task<int> RunBuild(CancellationToken cancellationToken) {
            try {
                using(await workspaceLock.LockAsync(cancellationToken)) {
                    await ExtractWorkspace(WorkspaceDir, workspacePipe.Reader.AsStream());
                    var buildTask = await buildStartTask.Task.WaitAsync(cancellationToken);

                    return await ExecBuildProcess(buildTask, cancellationToken);
                }
            }
            catch(Exception ex) {
                await buildOutputPipe.Writer.CompleteAsync();
                throw;
            }
        }

        private async Task ExtractWorkspace(string workspaceDir, Stream workspaceStream) {
            Directory.CreateDirectory(workspaceDir);
            await using var tarStream = new TarInputStream(workspaceStream);
            await ArchiveUtil.ExtractTar(tarStream, workspaceDir);
        }

        private async Task<int> ExecBuildProcess(BuildTaskBase buildTaskBase, CancellationToken cancellationToken) {
            var psi = buildTaskBase switch {
                BuildTask buildTask => CreateBuildProcess(buildTask),
                ContainerBuildTask containerBuildTask => CreateContainerBuildProcess(containerBuildTask),
                _ => throw new InvalidBuildTask()
            }; 
            
            var writeLock = new AsyncLock();

            async Task PipeData(Stream stream) {
                byte[] buffer = new byte[4096];
                int bytesRead;
                while((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0) {
                    using(await writeLock.LockAsync(cancellationToken)) {
                        await buildOutputPipe.Writer.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    }
                }
            }


            var process = Process.Start(psi);
            try {
                var exitTask = process.WaitForExitAsync();

                var stdoutTask = Task.Run(() => PipeData(process.StandardOutput.BaseStream), cancellationToken);
                await Task.Run(() => PipeData(process.StandardError.BaseStream), cancellationToken);
                await stdoutTask;

                await exitTask;

                await buildOutputPipe.Writer.CompleteAsync();

                return process.ExitCode;
            }
            catch {
                process.Kill();
                throw;
            }
        }

        private ProcessStartInfo CreateBuildProcess(BuildTask buildTask) {
            if(!PathUtil.IsValidSubPath(buildTask.BuildFile)) {
                throw new Exception("Invalid build file path.");
            }

            var psi = new ProcessStartInfo {
                FileName = "helium",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                ArgumentList = {
                    "build",

                    "--schema",
                    Path.Combine(WorkspaceDir, buildTask.BuildFile),

                    "--output",
                    ArtifactDir,

                    "--sources",
                    WorkspaceDir,
                    
                    "--current-dir",
                    Path.GetDirectoryName(buildTask.BuildFile),
                },
            };

            switch(buildTask.ReplayMode) {
                case ReplayMode.Discard:
                    break;
                    
                case ReplayMode.RecordCache:
                    psi.ArgumentList.Add("--archive");
                    psi.ArgumentList.Add(ReplayFile);
                    break;
                
                default:
                    throw new InvalidBuildTask();
            }
            
            psi.ArgumentList.Add(buildDir.Value);

            return psi;
        }

        private ProcessStartInfo CreateContainerBuildProcess(ContainerBuildTask containerBuildTask) {
            if(!PathUtil.IsValidSubPath(containerBuildTask.Dockerfile)) {
                throw new Exception("Invalid Dockerfile path.");
            }

            if(!PathUtil.IsValidSubPath(containerBuildTask.ImageFileName) ||
               containerBuildTask.ImageFileName.Contains(Path.DirectorySeparatorChar) ||
               containerBuildTask.ImageFileName.Contains(Path.AltDirectorySeparatorChar)) {
                throw new Exception("Invalid image file name.");
            }
            
            var psi = new ProcessStartInfo {
                FileName = "helium",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                ArgumentList = {
                    "container-build",
                    
                    "--os",
                    containerBuildTask.Platform.os.ToString(),
                    
                    "--arch",
                    containerBuildTask.Platform.arch.ToString(),

                    "--file",
                    Path.Combine(WorkspaceDir, containerBuildTask.Dockerfile),

                    "--build-context",
                    WorkspaceDir,
                },
            };

            switch(containerBuildTask.ReplayMode) {
                case ReplayMode.Discard:
                    break;
                    
                case ReplayMode.RecordCache:
                    psi.ArgumentList.Add("--archive");
                    psi.ArgumentList.Add(ReplayFile);
                    break;
                
                default:
                    throw new InvalidBuildTask();
            }
            
            psi.ArgumentList.Add(buildDir.Value);
            psi.ArgumentList.Add(containerBuildTask.ImageFileName);

            return psi;
        }
        
        public override Task OpenAsync(CancellationToken cancellationToken) =>
            transport.OpenAsync(cancellationToken);

        public override void Close() =>
            transport.Close();

        public override ValueTask<int> ReadAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken) =>
            transport.ReadAsync(buffer, offset, length, cancellationToken);

        public override Task WriteAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken) =>
            transport.WriteAsync(buffer, offset, length, cancellationToken);

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            transport.FlushAsync(cancellationToken);

        protected override void Dispose(bool disposing) {
            if(!disposing) {
                return;
            }

            using(workspaceLock.Lock()) {
                CurrentFileAccess?.Dispose();
                buildDir.DisposeAsync().AsTask().Wait();
            }
            
            transport.Dispose();
        }
    }
}