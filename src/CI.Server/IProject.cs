using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Helium.Pipeline;

namespace Helium.CI.Server
{
    public interface IProject
    {
        Guid Id { get; }
        
        ProjectConfig Config { get; }
        Task UpdateConfig(ProjectConfig config, CancellationToken cancellationToken);
        
        Task<PipelineLoader> GetPipelineLoader(CancellationToken cancellationToken);

        Task<IPipelineStatus?> GetPipelineStatus(int buildNum);

        IAsyncEnumerable<IPipelineStatus> AllPipelineStatus();

        Task<IPipelineStatus> StartBuild(PipelineInfo pipeline);

    }
}