using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
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
            buildRunTask = Task.Run(() => RunBuild(cancellationToken), cancellationToken);
        }

        private readonly AsyncLock workspaceLock = new AsyncLock();
        private readonly DirectoryCleanup<string> buildDir;
        private readonly TTransport transport;
        
        private readonly Pipe workspacePipe = new Pipe();
        private readonly TaskCompletionSource<BuildTask> buildStartTask = new TaskCompletionSource<BuildTask>();
        private readonly Pipe buildOutputPipe = new Pipe();
        private readonly TaskCompletionSource<Stream> buildOutputTcs = new TaskCompletionSource<Stream>();
        private readonly Task<int> buildRunTask;
        
        

        public PipeWriter WorkspacePipe => workspacePipe.Writer;
        public TaskCompletionSource<BuildTask> BuildTaskTCS => buildStartTask;
        public Task<Stream> BuildOutputStream => buildOutputTcs.Task;
        public Task<int> BuildResult => buildRunTask;
        public string ArtifactDir => Path.Combine(buildDir.Value, "artifacts");
        public string ReplayFile => Path.Combine(buildDir.Value, "replay.tar");

        public FileStream? CurrentFileAccess { get; set; }

        
        
        public override bool IsOpen => transport.IsOpen;
        

        private async Task<int> RunBuild(CancellationToken cancellationToken) {
            using(await workspaceLock.LockAsync(cancellationToken)) {
                await ExtractWorkspace(Path.Combine(buildDir.Value, "workspace"), workspacePipe.Reader.AsStream());
                var buildTask = await buildStartTask.Task.WaitAsync(cancellationToken);
                return await ExecBuild(buildOutputPipe.Writer, buildTask, cancellationToken);
            }
        }

        private async Task ExtractWorkspace(string workspaceDir, Stream workspaceStream) {
            Directory.CreateDirectory(workspaceDir);
            await using var tarStream = new TarInputStream(workspaceStream);
            await ArchiveUtil.ExtractTar(tarStream, workspaceDir);
        }

        private async Task<int> ExecBuild(PipeWriter writer, BuildTask buildTask, CancellationToken cancellationToken) {
            throw new NotImplementedException();
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