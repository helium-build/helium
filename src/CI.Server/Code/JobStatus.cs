using System;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace Helium.CI.Server
{
    internal class JobStatus : IJobStatus
    {
        public JobStatus(string artifactDir) {
            ArtifactDir = artifactDir;
        }
        
        public JobState State { get; private set; }
        public string ArtifactDir { get; }

        private readonly TaskCompletionSource<object?> complete = new TaskCompletionSource<object?>();

        public Task WaitForComplete(CancellationToken cancellationToken) => complete.Task.WaitAsync(cancellationToken);

        public async Task AppendOutput(byte[] statusOutput, CancellationToken cancellationToken) {
            
        }

        public void FailedWith(int exitCode) {
            State = JobState.Failed;
        }

        public void Completed() {
            State = JobState.Successful;
        }

        public void Error(Exception ex) {
            State = JobState.Error;
        }
    }
}