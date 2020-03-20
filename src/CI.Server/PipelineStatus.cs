using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Helium.Util;
using Nito.AsyncEx;

namespace Helium.CI.Server
{
    public class PipelineStatus : IPipelineStatus
    {
        public PipelineStatus(IReadOnlyDictionary<string, IJobStatus> jobsStatus, int buildNum) {
            JobsStatus = jobsStatus;
            BuildNumber = buildNum;

            foreach(var status in jobsStatus.Values) {
                status.JobStarted += JobStarted;
                status.JobCompleted += JobCompleted;
            }
        }


        private readonly AsyncLock outputLock = new AsyncLock();
        private GrowList<string> outputLines = GrowList<string>.Empty();
        private int completedJobs;
        private int failedJobs;

        public GrowList<string> OutputLines => outputLines;
        public event EventHandler? OutputLinesChanged;
        
        public int BuildNumber { get; }
        public IReadOnlyDictionary<string, IJobStatus> JobsStatus { get; }

        public event EventHandler? PipelineCompleted;


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
                    if(completed) {
                        if(failedJobs == 0) {
                            await WriteOutput("All jobs completed successfully.");
                        }
                        else {
                            await WriteOutput($"All jobs completed. {failedJobs} failed.");
                        }
                    }
                }

                if(completed) {
                    PipelineCompleted?.Invoke(this, EventArgs.Empty);
                }
            });
        }

        private async Task WriteOutput(string s) {
            outputLines.Add(s);
            OutputLinesChanged?.Invoke(this, EventArgs.Empty);
        }
       
        
    }
}