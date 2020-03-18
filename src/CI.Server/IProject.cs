using System;
using System.Threading;
using System.Threading.Tasks;

namespace Helium.CI.Server
{
    public interface IProject
    {
        Guid Id { get; }
        
        ProjectConfig Config { get; }
        Task UpdateConfig(ProjectConfig config, CancellationToken cancellationToken);
        
        Task<PipelineLoader> GetPipelineLoader(CancellationToken cancellationToken);

    }
}