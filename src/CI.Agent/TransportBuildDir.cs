using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
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
            buildRunTask = Task.Run(() => RunBuild(cancellationToken));
        }

        private readonly AsyncLock workspaceLock = new AsyncLock();
        private readonly DirectoryCleanup<string> buildDir;
        private readonly TTransport transport;
        
        private readonly Pipe workspacePipe = new Pipe();
        private readonly TaskCompletionSource<BuildTask> buildStartTask = new TaskCompletionSource<BuildTask>();
        private readonly Pipe buildOutputPipe = new Pipe();
        private readonly Stream buildOutputStream;
        private readonly Task<int> buildRunTask;
        
        

        public PipeWriter WorkspacePipe => workspacePipe.Writer;
        public TaskCompletionSource<BuildTask> BuildTaskTCS => buildStartTask;
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
                    return await ExecBuild(buildOutputPipe.Writer, buildTask, cancellationToken);
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

        private async Task<int> ExecBuild(PipeWriter writer, BuildTask buildTask, CancellationToken cancellationToken) {
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

                    "--archive",
                    ReplayFile,
                    
                    "--sources",
                    WorkspaceDir,
                    
                    "--current-dir",
                    Path.GetDirectoryName(buildTask.BuildFile),

                    buildDir.Value,
                },
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
                using(workspaceLock.Lock()) {
                    buildDir.DisposeAsync().AsTask().Wait();
                }
                
                CurrentFileAccess?.Dispose();
                
                transport.Dispose();
            }
        }
    }
}