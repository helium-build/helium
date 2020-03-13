using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Helium.CI.Server
{
    public interface IJobStatus
    {
        JobState State { get; }
        
        string ArtifactDir { get; }

        Task WaitForComplete(CancellationToken cancellationToken);
    }

    public enum JobState
    {
        Waiting,
        Running,
        Successful,
        Failed,
        Error,
    }
}