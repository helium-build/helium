using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Helium.Pipeline;

namespace Helium.CI.Server
{
    public interface IJobQueue
    {
        Task<IReadOnlyDictionary<BuildJob, IJobStatus>> Add(IPipelineRunManager pipelineRunManager, IEnumerable<BuildJob> job, CancellationToken cancellationToken);
        Task<IRunnableJob> AcceptJob(Func<BuildTask, Task<bool>> jobFilter, CancellationToken cancellationToken);
    }
}