using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Helium.Pipeline;

namespace Helium.CI.Server
{
    public interface IPipelineRunManager
    {
        string PipelineDir { get; }
        
        Task<string> GetInput(BuildInputSource inputSource, Func<BuildInputSource, Task<string>> f, CancellationToken cancellationToken);
        string NextInputPath();
        string BuildPath(BuildJob job);
    }
}