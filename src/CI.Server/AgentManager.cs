using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Helium.Util;
using Newtonsoft.Json;

namespace Helium.CI.Server
{
    public sealed class AgentManager : IAgentManager
    {
        private AgentManager(string agentsDir, IDictionary<Guid, IAgent> agents, CancellationToken cancellationToken) {
            this.agentsDir = agentsDir;
            this.agents = new ConcurrentDictionary<Guid, IAgent>(agents);
            this.cancellationToken = cancellationToken;
        }
        
        private readonly string agentsDir;
        private readonly ConcurrentDictionary<Guid, IAgent> agents;
        private readonly CancellationToken cancellationToken;


        public IAgent? GetAgent(Guid id) => agents.TryGetValue(id, out var agent) ? agent : null;

        public IReadOnlyCollection<IAgent> Agents => new ReadOnlyCollectionNoList<IAgent>(agents.Values);

        public async Task<IAgent> AddAgent(AgentConfig config) {
            Guid id;
            Agent agent;
            do {
                cancellationToken.ThrowIfCancellationRequested();
                id = Guid.NewGuid();
                var dir = Path.Combine(agentsDir, id.ToString());
                agent = new Agent(dir, id, config);
            } while(!agents.TryAdd(id, agent));

            await agent.WriteConfig(null, cancellationToken);

            return agent;
        }

        public async Task RemoveAgent(IAgent agent) {
            var agent2 = (Agent)agent;
            if(!agents.TryRemove(agent2.Id, out _)) {
                return;
            }

            Directory.Delete(agent2.AgentDir);
        }

        public static async Task<IAgentManager> Load(string agentsDir, CancellationToken cancellationToken) {
            var agents = new Dictionary<Guid, IAgent>();

            Directory.CreateDirectory(agentsDir);

            foreach(var agentDir in Directory.EnumerateDirectories(agentsDir)) {
                cancellationToken.ThrowIfCancellationRequested();
                
                if(!Guid.TryParse(Path.GetFileName(agentDir), out var id)) {
                    continue;
                }

                var configStr = await File.ReadAllTextAsync(Path.Combine(agentDir, "agent.json"), cancellationToken);
                var config = JsonConvert.DeserializeObject<AgentConfig>(configStr);
                
                var agent = new Agent(Path.GetFullPath(agentDir), id, config);
                agents[id] = agent;
            }
            
            return new AgentManager(agentsDir, agents, cancellationToken);
        }

        private sealed class Agent : IAgent
        {
            public Agent(string agentDir, Guid id, AgentConfig config) {
                AgentDir = agentDir;
                Id = id;
                this.config = config;
            }

            private volatile AgentConfig config;
            public AgentConfig Config => config;

            public string AgentDir { get; }
            public Guid Id { get; }

            public async Task WriteConfig(AgentConfig? config, CancellationToken cancellationToken) {
                if(config == null) config = Config;
                var dir = AgentDir;
                var tmpFile = Path.Combine(dir, "agent.json.tmp");
                var configStr = JsonConvert.SerializeObject(config);
                
                Directory.CreateDirectory(dir);
                
                await FileUtil.WriteAllTextToDiskAsync(tmpFile, configStr, Encoding.UTF8, cancellationToken);
                File.Move(tmpFile, Path.Combine(dir, "agent.json"), true);
            }


            public async Task UpdateConfig(AgentConfig config, CancellationToken cancellationToken) {
                await WriteConfig(config, cancellationToken);
                this.config = config;
            }
        }
        
    }
}