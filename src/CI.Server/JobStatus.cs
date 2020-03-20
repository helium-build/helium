using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Helium.Pipeline;
using Helium.Util;
using Nito.AsyncEx;

namespace Helium.CI.Server
{
    internal class JobStatus : IJobStatus
    {
        public JobStatus(string buildDir, BuildJob job) {
            Job = job;
            this.buildDir = buildDir;
        }
        
        private readonly string buildDir;
        private Stream? buildOutputStream;
        private volatile JobState state = JobState.Waiting;
        
        private GrowList<string> outputLines = GrowList<string>.Empty();
        private readonly Decoder outputDecoder = Encoding.UTF8.GetDecoder();
        private readonly StringBuilder currentLine = new StringBuilder();

        
        public BuildJob Job { get; }
        
        public JobState State => state;

        public event EventHandler<JobStartedEventArgs>? JobStarted;
        public event EventHandler<JobCompletedEventArgs>? JobCompleted;

        public string ArtifactDir => Path.Combine(buildDir, "artifacts");


        public GrowList<string> OutputLines => outputLines;
        public event EventHandler? OutputLinesChanged;


        private readonly TaskCompletionSource<object?> complete = new TaskCompletionSource<object?>();

        public Task WaitForComplete(CancellationToken cancellationToken) => complete.Task.WaitAsync(cancellationToken);

        public async Task AppendOutput(byte[] statusOutput, CancellationToken cancellationToken) {
            if(buildOutputStream == null) {
                Directory.CreateDirectory(buildDir);
                buildOutputStream = new FileStream(Path.Combine(buildDir, "output.log"), FileMode.Create,  FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            }

            DecodeLines(statusOutput);

            await buildOutputStream.WriteAsync(statusOutput, 0, statusOutput.Length, cancellationToken);
        }

        private void DecodeLines(byte[] statusOutput) {
            var decoded = new char[outputDecoder.GetCharCount(statusOutput, 0, statusOutput.Length)];
            outputDecoder.GetChars(statusOutput, 0, statusOutput.Length, decoded, 0);

            bool triggerEvent = AppendLineChars(decoded);

            if(triggerEvent) {
                OutputLinesChanged?.Invoke(this, EventArgs.Empty);
            }
            
            
        }

        private async Task FinishOutput() {
            if(buildOutputStream != null) await buildOutputStream.DisposeAsync();

            int extraChars = outputDecoder.GetCharCount(Array.Empty<byte>(), 0, 0, flush: true);
            if(extraChars > 0) {
                var decoded = new char[extraChars];
                outputDecoder.GetChars(Array.Empty<byte>(), 0, 0, decoded, 0, flush: true);
                AppendLineChars(decoded);
            }
            
            outputLines.Add(currentLine.ToString());
            OutputLinesChanged?.Invoke(this, EventArgs.Empty);
        }

        private bool AppendLineChars(char[] decoded) {
            bool triggerEvent = false;
            foreach(var c in decoded) {
                switch(c) {
                    case '\r':
                        break;

                    case '\n':
                        triggerEvent = true;
                        outputLines.Add(currentLine.ToString());
                        currentLine.Clear();
                        break;

                    default:
                        currentLine.Append(c);
                        break;
                }
            }

            return triggerEvent;
        }

        public void Started(AgentConfig agent) {
            state = JobState.Successful;
            JobStarted?.Invoke(this, new JobStartedEventArgs(agent));
        }

        public Stream OpenReplay() {
            Directory.CreateDirectory(buildDir);
            return File.Create(Path.Combine(buildDir, "replay.tar"));
        }
        

        public async Task FailedWith(int exitCode) {
            await FinishOutput();
            state = JobState.Failed;
            JobCompleted?.Invoke(this, new JobCompletedEventArgs(exitCode, null));
        }

        public async Task Completed() {
            await FinishOutput();
            state = JobState.Successful;
            JobCompleted?.Invoke(this, new JobCompletedEventArgs(0, null));
        }

        public async Task Error(Exception ex) {
            await FinishOutput();
            state = JobState.Error;
            JobCompleted?.Invoke(this, new JobCompletedEventArgs(1, null));
        }
    }
}