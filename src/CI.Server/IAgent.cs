using System;
using System.Threading;
using System.Threading.Tasks;

namespace Helium.CI.Server
{
    public interface IAgent
    {
        Guid Id { get; }
        AgentConfig Config { get; }
        Task UpdateConfig(AgentConfig config, CancellationToken cancellationToken);
    }
}