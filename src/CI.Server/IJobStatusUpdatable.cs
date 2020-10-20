using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Helium.CI.Server
{
    public interface IJobStatusUpdatable : IJobStatus
    {
        void Started(AgentConfig agent);
        Task AppendOutput(byte[] buffer, CancellationToken cancellationToken);
        Task FailedWith(int exitCode);
        Task Error(Exception ex);
        Task Completed();
        Stream OpenReplay();
        void Cancel();
    }
}