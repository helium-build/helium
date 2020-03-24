using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Helium.Pipeline;

namespace Helium.CI.Server
{
    public interface IJobQueue
    {
        Task<IPipelineStatus> Add(IPipelineRunManager pipelineRunManager, IEnumerable<BuildJob> job, int buildNum, CancellationToken cancellationToken);
        Task<IRunnableJob> AcceptJob(Func<BuildTaskBase, Task<bool>> jobFilter, CancellationToken cancellationToken);
    }
}