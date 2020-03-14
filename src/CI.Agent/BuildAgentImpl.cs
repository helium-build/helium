using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Helium.CI.Common.Protocol;
using Helium.Sdks;
using Helium.Util;
using Newtonsoft.Json;
using Nito.AsyncEx;

namespace Helium.CI.Agent
{
    public class BuildAgentImpl : BuildAgent.IAsync
    {
        private readonly AsyncLock mutex = new AsyncLock();
        private AgentState state = AgentState.Initial;
        
        
        
        public async Task<bool> supportsPlatformAsync(string platform, CancellationToken cancellationToken = default(CancellationToken)) {
            var platformInfo = JsonConvert.DeserializeObject<PlatformInfo>(platform);
            return PlatformInfo.Current.SupportsRunning(platformInfo);
        }

        public async Task sendWorkspaceAsync(byte[] chunk, CancellationToken cancellationToken = default(CancellationToken)) {
            using(await mutex.LockAsync()) {
                if(state == AgentState.Initial) {
                    
                }
            }
        }

        public Task startBuildAsync(string task, CancellationToken cancellationToken = default(CancellationToken)) {
            throw new System.NotImplementedException();
        }

        public Task<BuildStatus> getStatusAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            throw new System.NotImplementedException();
        }

        public Task<int> getExitCodeAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            throw new System.NotImplementedException();
        }

        public Task<List<string>> artifactsAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            throw new System.NotImplementedException();
        }

        public Task openOutputAsync(OutputType type, string name, CancellationToken cancellationToken = default(CancellationToken)) {
            throw new System.NotImplementedException();
        }

        public Task<byte[]> readOutputAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            throw new System.NotImplementedException();
        }
    }
}