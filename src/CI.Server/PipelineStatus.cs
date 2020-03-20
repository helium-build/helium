using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Helium.Util;
using Newtonsoft.Json;
using Nito.AsyncEx;

namespace Helium.CI.Server
{
    public class PipelineStatus : IPipelineStatus
    {
        public PipelineStatus(IReadOnlyDictionary<string, IJobStatus> jobsStatus, int buildNum, string buildDir) {
            this.buildDir = buildDir;
            JobsStatus = jobsStatus;
            BuildNumber = buildNum;

            foreach(var status in jobsStatus.Values) {
                status.JobStarted += JobStarted;
                status.JobCompleted += JobCompleted;
            }
        }

        
        private readonly string buildDir;

        private readonly AsyncLock outputLock = new AsyncLock();
        private StreamWriter? outputStreamWriter;
        private GrowList<string> outputLines = GrowList<string>.Empty();
        private int completedJobs;
        private int failedJobs;

        
        public int BuildNumber { get; }

        private readonly object stateLock = new object();
        private BuildState state = BuildState.Running;
        public BuildState State {
            get{
                lock(stateLock) {
                    return state;
                }
            }
        }

        public IReadOnlyDictionary<string, IJobStatus> JobsStatus { get; }

        public event EventHandler? PipelineCompleted;


        public async Task<GrowList<string>> OutputLines() {
            return outputLines;
        }

        public event EventHandler<OutputLinesChangedEventArgs>? OutputLinesChanged;
        
        private void JobStarted(object? sender, JobStartedEventArgs e) {
            if(!(sender is IJobStatus status)) return;
            string message = $"Started job {status.Job.Id} on agent {e.Agent.Name}.";
            
            Task.Run(() => WriteOutput(message));
        }

        private void JobCompleted(object? sender, JobCompletedEventArgs e) {
            if(!(sender is IJobStatus status)) return;

            Task.Run(async () => {
                bool completed;
                using(await outputLock.LockAsync()) {
                    ++completedJobs;
                    if(e.Exception != null) {
                        ++failedJobs;
                        await WriteOutput($"Error occurred while running job {status.Job.Id}.");
                    }
                    else if(e.ExitCode != 0) {
                        ++failedJobs;
                        await WriteOutput($"Job {status.Job.Id} exited with error code {e.ExitCode}.");
                    }
                    else {
                        await WriteOutput($"Job {status.Job.Id} completed successfully.");
                    }

                    completed = (completedJobs == JobsStatus.Count);
                    lock(stateLock) {
                        state = failedJobs == 0 ? BuildState.Successful : BuildState.Failed;
                    }
                    if(completed) {
                        if(failedJobs == 0) {
                            await WriteOutput("All jobs completed successfully.");
                        }
                        else {
                            await WriteOutput($"All jobs completed. {failedJobs} failed.");
                        }

                        await CloseOutput();

                        var result = new PipelineBuildResult {
                            State = state,
                        };
                        
                        await FileUtil.WriteAllTextToDiskAsync(
                            Path.Combine(buildDir, "result.json"),
                            JsonConvert.SerializeObject(result),
                            Encoding.UTF8,
                            CancellationToken.None
                        );
                    }
                }

                if(completed) {
                    PipelineCompleted?.Invoke(this, EventArgs.Empty);
                }
            });
        }

        private async Task WriteOutput(string s) {
            if(outputStreamWriter == null) {
                outputStreamWriter = new StreamWriter(new FileStream(Path.Combine(buildDir, "output.log"), FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan));
            }

            await outputStreamWriter.WriteLineAsync(s);
            outputLines.Add(s);
            OutputLinesChanged?.Invoke(this, new OutputLinesChangedEventArgs(outputLines));
        }

        private async Task CloseOutput() {
            if(outputStreamWriter == null) {
                return;
            }
            
            await outputStreamWriter.FlushAsync();
            ((FileStream)outputStreamWriter.BaseStream).Flush(true);
            await outputStreamWriter.DisposeAsync();
        }

       
        
    }
}