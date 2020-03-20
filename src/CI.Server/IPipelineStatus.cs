using System;
using System.Collections.Generic;
using Helium.Pipeline;
using Helium.Util;

namespace Helium.CI.Server
{
    public interface IPipelineStatus
    {
        int BuildNumber { get; }
        
        GrowList<string> OutputLines { get; }
        
        event EventHandler OutputLinesChanged;
        
        IReadOnlyDictionary<string, IJobStatus> JobsStatus { get; }

        event EventHandler PipelineCompleted;
    }
}