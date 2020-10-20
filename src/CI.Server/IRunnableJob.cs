using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Helium.CI.Common;
using Helium.Pipeline;

namespace Helium.CI.Server
{
    public interface IRunnableJob
    {
        Task WriteWorkspace(Stream stream, CancellationToken cancellationToken);
        BuildTaskBase BuildTask { get; }
        IJobStatusUpdatable JobStatus { get; }
    }
}