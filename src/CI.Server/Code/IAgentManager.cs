using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Helium.CI.Server
{
    public interface IAgentManager
    {
        IAgent? GetAgent(Guid id);
        IEnumerable<IAgent> AllAgents();

        Task<IAgent> AddAgent(AgentConfig config);
        Task RemoveAgent(IAgent agent);
    }
}