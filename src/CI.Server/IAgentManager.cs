using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Helium.CI.Server
{
    public interface IAgentManager
    {
        IAgent? GetAgent(Guid id);
        IReadOnlyCollection<IAgent> Agents { get; }

        IAgent? Authenticate(string key) => Agents.FirstOrDefault(agent => agent.Config.Key == key);

        Task<IAgent> AddAgent(AgentConfig config);
        Task RemoveAgent(IAgent agent);
    }
}