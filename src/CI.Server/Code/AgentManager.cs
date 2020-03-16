using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Helium.Util;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Helium.CI.Server
{
    public class AgentManager : IAgentManager
    {
        private AgentManager(string agentsDir, IJobQueue jobQueue, ServerConfig serverConfig, IDictionary<Guid, IAgent> agents, CancellationToken cancellationToken) {
            this.agentsDir = agentsDir;
            this.jobQueue = jobQueue;
            this.serverConfig = serverConfig;
            this.agents = new ConcurrentDictionary<Guid, IAgent>(agents);
            this.cancellationToken = cancellationToken;
        }
        
        private readonly string agentsDir;
        private readonly IJobQueue jobQueue;
        private readonly ServerConfig serverConfig;
        private readonly ConcurrentDictionary<Guid, IAgent> agents;
        private readonly CancellationToken cancellationToken;


        public IAgent? GetAgent(Guid id) => agents.TryGetValue(id, out var agent) ? agent : null;

        public IEnumerable<IAgent> AllAgents() => agents.Values;

        public async Task<IAgent> AddAgent(AgentConfig config) {
            Guid id;
            Agent agent;
            do {
                cancellationToken.ThrowIfCancellationRequested();
                id = Guid.NewGuid();
                var dir = Path.Combine(agentsDir, id.ToString());
                agent = new Agent(dir, jobQueue, serverConfig, config);
            } while(!agents.TryAdd(id, agent));

            await agent.WriteConfig(null, cancellationToken);

            return agent;
        }

        private class Agent : IAgent
        {
            public Agent(string agentDir, IJobQueue jobQueue, ServerConfig serverConfig, AgentConfig config) {
                AgentDir = agentDir;
                exec = new AgentExecutor(jobQueue, config, serverConfig);
            }
            
            private readonly AgentExecutor exec;

            public AgentConfig Config => exec.Config;

            public string AgentDir { get; }

            public async Task WriteConfig(AgentConfig? config, CancellationToken cancellationToken) {
                if(config == null) config = Config;
                var dir = AgentDir;
                var tmpFile = Path.Combine(dir, "agent.json.tmp");
                var configStr = JsonConvert.SerializeObject(config);
                
                Directory.CreateDirectory(dir);
                
                await FileUtil.WriteAllTextToDiskAsync(tmpFile, configStr, Encoding.UTF8, cancellationToken);
                File.Move(tmpFile, Path.Combine(dir, "agent.json"), true);
;            }


            public async Task UpdateConfig(AgentConfig config, CancellationToken cancellationToken) {
                await WriteConfig(config, cancellationToken);
                await exec.UpdateConfig(config, cancellationToken);
            }
        }

        public async Task RemoveAgent(IAgent agent) {
            Directory.Delete(((Agent)agent).AgentDir);
        }

        public static async Task<IAgentManager> Load(string agentsDir, IJobQueue jobQueue, ServerConfig serverConfig, CancellationToken cancellationToken) {
            var agents = new Dictionary<Guid, IAgent>();

            foreach(var agentDir in Directory.EnumerateDirectories(agentsDir)) {
                cancellationToken.ThrowIfCancellationRequested();
                
                if(!Guid.TryParse(Path.GetFileName(agentDir), out var id)) {
                    continue;
                }

                var configStr = await File.ReadAllTextAsync(Path.Combine(agentDir, "agent.json"), cancellationToken);
                var config = JsonConvert.DeserializeObject<AgentConfig>(configStr);
                
                agents[id] = new Agent(Path.GetFullPath(agentDir), jobQueue, serverConfig, config);
            }
            
            return new AgentManager(agentsDir, jobQueue, serverConfig, agents, cancellationToken);
        }
        
    }
}