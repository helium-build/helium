using System.Threading;
using System.Threading.Tasks;

namespace Helium.CI.Server
{
    public interface IAgent
    {
        AgentConfig Config { get; }
        Task UpdateConfig(AgentConfig config, CancellationToken cancellationToken);
    }
}