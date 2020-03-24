using System;
using System.Data;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Helium.Pipeline;
using Helium.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Nito.AsyncEx;

namespace Helium.CI.Server
{
    internal class JobStatus : IJobStatus
    {
        public JobStatus(string buildDir, BuildJob job) {
            this.buildDir = buildDir;
            this.job = job;
        }
        
        private readonly string buildDir;
        private readonly BuildJob job;
        private FileStream? buildOutputStream;
        private volatile BuildState state = BuildState.Waiting;
        
        private GrowList<string> outputLines = GrowList<string>.Empty();
        private readonly Decoder outputDecoder = Encoding.UTF8.GetDecoder();
        private readonly StringBuilder currentLine = new StringBuilder();


        public string Id => job.Id;
        public BuildTaskBase BuildTask => job.Task;

        public BuildState State => state;

        public event EventHandler<JobStartedEventArgs>? JobStarted;
        public event EventHandler<JobCompletedEventArgs>? JobCompleted;

        public string ArtifactDir => Path.Combine(buildDir, "artifacts");


        public async Task<GrowList<string>> OutputLines() => outputLines;

        public event EventHandler<OutputLinesChangedEventArgs>? OutputLinesChanged;


        private readonly TaskCompletionSource<object?> complete = new TaskCompletionSource<object?>();

        public Task WaitForComplete(CancellationToken cancellationToken) => complete.Task.WaitAsync(cancellationToken);


        public async Task WriteBuildJobFile() {
            Directory.CreateDirectory(buildDir);
            await FileUtil.WriteAllTextToDiskAsync(
                Path.Combine(buildDir, "task.json"),
                JsonConvert.SerializeObject(BuildTask),
                Encoding.UTF8,
                CancellationToken.None
            );
        }

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
                OutputLinesChanged?.Invoke(this, new OutputLinesChangedEventArgs(outputLines));
            }
            
            
        }

        private async Task FinishOutput(BuildState buildState, JobCompletedEventArgs jobCompletedEventArgs) {
            if(buildOutputStream != null) {
                await buildOutputStream.FlushAsync();
                buildOutputStream.Flush(flushToDisk: true);
                await buildOutputStream.DisposeAsync();
            }

            int extraChars = outputDecoder.GetCharCount(Array.Empty<byte>(), 0, 0, flush: true);
            if(extraChars > 0) {
                var decoded = new char[extraChars];
                outputDecoder.GetChars(Array.Empty<byte>(), 0, 0, decoded, 0, flush: true);
                AppendLineChars(decoded);
            }

            await FileUtil.WriteAllTextToDiskAsync(
                Path.Combine(buildDir, "result.json"),
                JsonConvert.SerializeObject(new JobBuildResult {
                    State = buildState,
                }),
                Encoding.UTF8,
                CancellationToken.None
            );
            
            outputLines.Add(currentLine.ToString());
            OutputLinesChanged?.Invoke(this, new OutputLinesChangedEventArgs(outputLines));

            state = buildState;
            JobCompleted?.Invoke(this, jobCompletedEventArgs);
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
            state = BuildState.Running;
            JobStarted?.Invoke(this, new JobStartedEventArgs(agent));
        }

        public Stream OpenReplay() {
            Directory.CreateDirectory(buildDir);
            return File.Create(Path.Combine(buildDir, "replay.tar"));
        }
        

        public Task FailedWith(int exitCode) =>
            FinishOutput(BuildState.Failed, new JobCompletedEventArgs(exitCode, null));

        public Task Completed() => 
            FinishOutput(BuildState.Successful, new JobCompletedEventArgs(0, null));

        public Task Error(Exception ex) =>
            FinishOutput(BuildState.Error, new JobCompletedEventArgs(1, null));
    }
}