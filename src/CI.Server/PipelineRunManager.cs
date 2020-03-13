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
            this.pipelineRunDir = pipelineRunDir;
        }

        private readonly string pipelineRunDir;
        private readonly ConcurrentDictionary<BuildInputSource, Task<string>> inputCache = new ConcurrentDictionary<BuildInputSource, Task<string>>();
        
        private int nextInputPathId = 0;
        private int nextArtifactDirId = 0;

        public Task<string> GetInput(BuildInputSource inputSource, Func<BuildInputSource, Task<string>> f, CancellationToken cancellationToken) =>
            inputCache.GetOrAdd(inputSource, f);

        public string NextInputPath() {
            int id = Interlocked.Increment(ref nextInputPathId);
            return Path.Combine(pipelineRunDir, "inputs", "input" + id);
        }

        public string NextArtifactDir() {
            int id = Interlocked.Increment(ref nextArtifactDirId);
            return Path.Combine(pipelineRunDir, "job-artifacts", "job" + id);
        }
    }
}