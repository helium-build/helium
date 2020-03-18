using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Helium.Pipeline
{
    public sealed class PipelineBuilderState
    {
        
        public List<BuildArgInfo> BuildArgs = new List<BuildArgInfo>();
        
        public void AddArgument(IDictionary<string, object> argInfo) {
            var arg = new BuildArgInfo(
                name: (string)argInfo["name"],
                description: argInfo.TryGetValue("description", out var description) ? (string)description : null,
                required: argInfo.TryGetValue("required", out var required) && (bool)required
            );
            
            BuildArgs.Add(arg);
        }
        
        public Func<IReadOnlyDictionary<string, string>, PipelineInfo?>? PipelineBuilder { get; set; }
    }

    public class BuildArgInfo
    {
        public BuildArgInfo(string name, string? description, bool required) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description;
            Required = required;
        }
        
        public string Name { get; }
        public string? Description { get; }
        public bool Required { get; }
    }
}