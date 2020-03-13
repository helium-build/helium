using System;
using System.Threading;
using System.Threading.Tasks;
using Helium.Pipeline;

namespace Helium.CI.Server
{
    public interface IPipelineManager
    {
        Task<string> GetInput(BuildInputSource inputSource, Func<BuildInputSource, Task<string>> f, CancellationToken cancellationToken);
        string NextInputPath();
        string NextArtifactDir();
    }
}