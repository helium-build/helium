using System.Threading;
using System.Threading.Tasks;
using Helium.CI.Common.Protocol;

namespace Helium.CI.Server
{
    public interface IRunnableJob
    {
        Task Run(BuildAgent.IAsync agent, AgentConfig agentConfig, CancellationToken cancellationToken);

        void CancelBuild();
    }
}