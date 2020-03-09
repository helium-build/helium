using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Helium.Pipeline
{
    public sealed class BuildJob
    {
        public BuildJob(BuildTask task, IEnumerable<BuildInput>? input = null) {
            Task = task ?? throw new ArgumentNullException(nameof(task));
            Input = new ReadOnlyCollection<BuildInput>((input ?? Enumerable.Empty<BuildInput>()).ToList());
        }
        
        public BuildJob(IDictionary<string, object> obj)
            : this(
                task: (BuildTask)obj["task"],
                input: obj.TryGetValue("input", out var inputObj) ? ((IEnumerable)inputObj).Cast<BuildInput>() : null
            ) {}
        
        public BuildTask Task { get; }
        public IReadOnlyList<BuildInput> Input { get; }
    }
}