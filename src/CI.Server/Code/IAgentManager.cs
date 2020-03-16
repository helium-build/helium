using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Helium.CI.Server
{
    public interface IAgentManager
    {
        IAgent? GetAgent(Guid id);
        IReadOnlyCollection<IAgent> Agents { get; }

        Task<IAgent> AddAgent(AgentConfig config);
        Task RemoveAgent(IAgent agent);
    }
}