using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Helium.CI.Common;
using Helium.Pipeline;
using Helium.Sdks;

namespace Helium.CI.Server
{
    public interface IJobQueue
    {
        Task<IPipelineStatus> AddJobs(IPipelineRunManager pipelineRunManager, IEnumerable<BuildJob> job, int buildNum, CancellationToken cancellationToken);
        Task<IRunnableJob> AcceptJob(PlatformChecker platformChecker, CancellationToken cancellationToken);
    }
}