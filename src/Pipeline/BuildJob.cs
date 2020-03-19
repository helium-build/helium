using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace Helium.Pipeline
{
    public sealed class BuildJob
    {
        public BuildJob(string id, BuildTask task, IEnumerable<BuildInput>? input = null) {
            if(!Regex.IsMatch(id, @"[a-z0-9\-_]+")) {
                throw new Exception("Invalid id.");
            }

            Id = id ?? throw new ArgumentNullException(nameof(id));
            Task = task ?? throw new ArgumentNullException(nameof(task));
            Input = new ReadOnlyCollection<BuildInput>((input ?? Enumerable.Empty<BuildInput>()).ToList());

        }
        
        public BuildJob(IDictionary<string, object> obj)
            : this(
                id: (string)obj["id"],
                task: (BuildTask)obj["task"],
                input: obj.TryGetValue("input", out var inputObj) ? ((IEnumerable)inputObj).Cast<BuildInput>() : null
            ) {}

        public string Id { get; }
        public BuildTask Task { get; }
        public IReadOnlyList<BuildInput> Input { get; }
    }
}