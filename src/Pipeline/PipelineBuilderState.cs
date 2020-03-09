using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Helium.Pipeline
{
    public sealed class PipelineBuilderState
    {
        public PipelineBuilderState(IReadOnlyDictionary<string, string>? arguments = null) {
            Arguments = new ReadOnlyDictionary<string, string>(
                arguments?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, string>()
            );
        }
        
        public IReadOnlyDictionary<string, string> Arguments { get; }
        
        public PipelineInfo? Pipeline { get; set; }
    }
}