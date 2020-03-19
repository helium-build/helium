using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace Helium.CI.Server
{
    internal class JobStatus : IJobStatus
    {
        public JobStatus(string buildDir) {
            this.buildDir = buildDir;
        }
        
        private readonly string buildDir;
        private Stream? buildOutputStream;

        public JobState State { get; private set; } = JobState.Waiting;
        public string ArtifactDir => Path.Combine(buildDir, "artifacts");

        private readonly TaskCompletionSource<object?> complete = new TaskCompletionSource<object?>();

        public Task WaitForComplete(CancellationToken cancellationToken) => complete.Task.WaitAsync(cancellationToken);

        public async Task AppendOutput(byte[] statusOutput, CancellationToken cancellationToken) {
            if(buildOutputStream == null) {
                Directory.CreateDirectory(buildDir);
                buildOutputStream = new FileStream(Path.Combine(buildDir, "output.log"), FileMode.Create,  FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            }

            await buildOutputStream.WriteAsync(statusOutput, 0, statusOutput.Length, cancellationToken);
        }

        public Stream OpenReplay() {
            Directory.CreateDirectory(buildDir);
            return File.Create(Path.Combine(buildDir, "replay.tar"));
        }
        

        public async Task FailedWith(int exitCode) {
            if(buildOutputStream != null) await buildOutputStream.DisposeAsync();
            State = JobState.Failed;
        }

        public async Task Completed() {
            if(buildOutputStream != null) await buildOutputStream.DisposeAsync();
            State = JobState.Successful;
        }

        public async Task Error(Exception ex) {
            if(buildOutputStream != null) await buildOutputStream.DisposeAsync();
            State = JobState.Error;
        }
    }
}