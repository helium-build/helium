using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Helium.Pipeline;

namespace Helium.CI.Server
{
    internal class PipelineRunManager : IPipelineRunManager
    {
        public PipelineRunManager(string pipelineRunDir) {
            PipelineDir = pipelineRunDir;
        }

        private readonly ConcurrentDictionary<BuildInputSource, Task<string>> inputCache = new ConcurrentDictionary<BuildInputSource, Task<string>>();
        
        private int nextInputPathId = 0;

        public string PipelineDir { get; }

        public Task<string> GetInput(BuildInputSource inputSource, Func<BuildInputSource, Task<string>> f, CancellationToken cancellationToken) =>
            inputCache.GetOrAdd(inputSource, f);

        public string NextInputPath() {
            int id = Interlocked.Increment(ref nextInputPathId);
            return Path.Combine(PipelineDir, "inputs", "input" + id);
        }

        public string BuildPath(BuildJob job) => Path.Combine(PipelineDir, "jobs", job.Id);

    }
}