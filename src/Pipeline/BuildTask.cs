using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Helium.Sdks;

namespace Helium.Pipeline
{
    public sealed class BuildTask
    {
        public BuildTask(string buildFile, PlatformInfo platform, IReadOnlyDictionary<string, string>? arguments = null,
            IEnumerable<SdkInfo>? extraSdks = null) {

            BuildFile = buildFile ?? throw new ArgumentNullException(nameof(buildFile));
            Platform = platform ?? throw new ArgumentNullException(nameof(platform));
            Arguments = new ReadOnlyDictionary<string, string>(
                arguments?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, string>()
            );
            ExtraSdks = new ReadOnlyCollection<SdkInfo>((extraSdks ?? Enumerable.Empty<SdkInfo>()).ToList());

        }
        
        public BuildTask(IDictionary<string, object> obj)
            : this(
                buildFile: (string)obj["buildFile"],
                platform: (PlatformInfo)obj["platform"],
                arguments: obj.TryGetValue("arguments", out var arguments)
                    ? ((IDictionary<string, object>)arguments).ToDictionary(kvp => kvp.Key, kvp => (string)kvp.Value)
                    : null,
                extraSdks: obj.TryGetValue("extraSdks", out var extraSdks)
                    ? ((IEnumerable)extraSdks).Cast<SdkInfo>()
                    : null
            ) {}
        
        public string BuildFile { get; }
        
        public PlatformInfo Platform { get; }
        
        public IReadOnlyDictionary<string, string> Arguments { get; }
        public IReadOnlyList<SdkInfo> ExtraSdks { get; }
    }
}