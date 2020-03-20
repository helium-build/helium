using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Helium.Pipeline;
using Helium.Util;

namespace Helium.CI.Server
{
    public interface IJobStatus
    {
        BuildJob Job { get; }
        
        BuildState State { get; }

        event EventHandler<JobStartedEventArgs> JobStarted;
        event EventHandler<JobCompletedEventArgs> JobCompleted;
        
        string ArtifactDir { get; }
        
        GrowList<string> OutputLines { get; }

        event EventHandler OutputLinesChanged;

        Task WaitForComplete(CancellationToken cancellationToken);
    }

    public class JobCompletedEventArgs
    {
        public JobCompletedEventArgs(int exitCode, Exception? exception) {
            ExitCode = exitCode;
            Exception = exception;
        }

        public int ExitCode { get; }
        public Exception? Exception { get; }
    }

    public class JobStartedEventArgs
    {
        public JobStartedEventArgs(AgentConfig agent) {
            Agent = agent;
        }

        public AgentConfig Agent { get; }
    }
}