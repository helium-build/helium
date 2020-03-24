using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Helium.Pipeline;
using Helium.Util;
using Newtonsoft.Json;

namespace Helium.CI.Server
{
    internal class CompletedJobStatus : IJobStatus
    {
        public CompletedJobStatus(string dir, string id, BuildTaskBase task, BuildState state) {
            this.dir = dir;
            Id = id;
            BuildTask = task;
            State = state;
        }

        private readonly string dir;


        public string Id { get; }
        public BuildTaskBase BuildTask { get; }
        public BuildState State { get; }
        
        public event EventHandler<JobStartedEventArgs> JobStarted {
            add {}
            remove {}
        }
        public event EventHandler<JobCompletedEventArgs> JobCompleted {
            add {}
            remove {}
        }

        public string ArtifactDir => Path.Combine(dir, "artifacts");

        public async Task<GrowList<string>> OutputLines() {
            try {
                var lines = await File.ReadAllLinesAsync(Path.Combine(dir, "output.log"));
                return new GrowList<string>(lines);
            }
            catch(IOException) {
                return default;
            }
        }

        public event EventHandler<OutputLinesChangedEventArgs> OutputLinesChanged {
            add {}
            remove {}
        }
        
        public Task WaitForComplete(CancellationToken cancellationToken) => Task.CompletedTask;

        public static async Task<IJobStatus> Load(string dir, string id) {
            var task = JsonConvert.DeserializeObject<BuildTaskBase>(
                await File.ReadAllTextAsync(Path.Combine(dir, "task.json"))
            );
            
            BuildState state = BuildState.Failed;
            try {
                var result = JsonConvert.DeserializeObject<JobBuildResult>(
                    await File.ReadAllTextAsync(Path.Combine(dir, "result.json"))
                );

                state = result.State;
            }
            catch(IOException) {}
            
            return new CompletedJobStatus(dir, id, task, state);
        }
    }
}