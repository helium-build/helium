using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Helium.Pipeline;
using Helium.Util;

namespace Helium.CI.Server
{
    public interface IPipelineStatus
    {
        int BuildNumber { get; }
        
        BuildState State { get; }
        
        IReadOnlyDictionary<string, IJobStatus> JobsStatus { get; }

        event EventHandler PipelineCompleted;

        Task<GrowList<string>> OutputLines();
        
        event EventHandler<OutputLinesChangedEventArgs> OutputLinesChanged;
    }
}