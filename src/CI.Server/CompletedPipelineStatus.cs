using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Helium.Util;
using Newtonsoft.Json;

namespace Helium.CI.Server
{
    internal class CompletedPipelineStatus : IPipelineStatus
    {
        private CompletedPipelineStatus(string dir, int buildNumber, IReadOnlyDictionary<string, IJobStatus> jobsStatus, BuildState state) {
            this.dir = dir;
            BuildNumber = buildNumber;
            JobsStatus = jobsStatus;
            State = state;
        }
        
        private readonly string dir;

        public int BuildNumber { get; }
        public IReadOnlyDictionary<string, IJobStatus> JobsStatus { get; }
        public BuildState State { get; }

        public event EventHandler PipelineCompleted {
            add {}
            remove {}
        }
        
        public async Task<GrowList<string>> OutputLines() {
            var lines = await File.ReadAllLinesAsync(Path.Combine(dir, "output.log"));
            return new GrowList<string>(lines);
        }

        public event EventHandler<OutputLinesChangedEventArgs> OutputLinesChanged {
            add {}
            remove { }
        }

        public static async Task<IPipelineStatus> Load(string dir, int buildNumber) {
            var jobsDir = Path.Combine(dir, "jobs");
            var jobs = await Directory.EnumerateDirectories(jobsDir)
                .ToAsyncEnumerable()
                .SelectAwait(async jobDir => await CompletedJobStatus.Load(jobDir, Path.GetFileName(jobDir)))
                .ToDictionaryAsync(jobStatus => jobStatus.Id);

            BuildState state = BuildState.Failed;
            try {
                var result = JsonConvert.DeserializeObject<PipelineBuildResult>(
                    await File.ReadAllTextAsync(Path.Combine(dir, "result.json"))
                );

                state = result.State;
            }
            catch(IOException) {}
            
            return new CompletedPipelineStatus(dir, buildNumber, jobs, state);
        }
    }
}