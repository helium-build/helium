using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Helium.Pipeline
{
    public sealed class PipelineInfo
    {
        public PipelineInfo(IEnumerable<BuildJob> buildJobs) {
            if(buildJobs == null) throw new ArgumentNullException(nameof(buildJobs));
            
            BuildJobs = new ReadOnlyCollection<BuildJob>(
                buildJobs.ToList()
            );
        }
        
        public PipelineInfo(IDictionary<string, object> obj)
            : this(
                buildJobs: ((IEnumerable)obj["buildJobs"]).Cast<BuildJob>()
            ) {}
        
        public IReadOnlyList<BuildJob> BuildJobs { get; }
    }
}