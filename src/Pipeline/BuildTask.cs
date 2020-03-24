using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Helium.Sdks;
using JsonSubTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Helium.Pipeline
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ReplayMode
    {
        Discard,
        RecordBuild,
        RecordCache,
        RecordFull,
    }

    [JsonConverter(typeof(JsonSubtypes))]
    [JsonSubtypes.KnownSubTypeWithProperty(typeof(BuildTask), nameof(BuildTask.BuildFile))]
    [JsonSubtypes.KnownSubTypeWithProperty(typeof(ContainerBuildTask), nameof(ContainerBuildTask.Dockerfile))]
    public abstract class BuildTaskBase
    {
        internal BuildTaskBase() {}
        
        public abstract PlatformInfo Platform { get; }
        public abstract ReplayMode ReplayMode { get; }
    }

    public sealed class BuildTask : BuildTaskBase
    {
        [JsonConstructor]
        public BuildTask(
            string buildFile,
            PlatformInfo platform,
            IReadOnlyDictionary<string, string>? arguments = null,
            IEnumerable<SdkInfo>? extraSdks = null,
            ReplayMode? replayMode = null
        ) {

            BuildFile = buildFile ?? throw new ArgumentNullException(nameof(buildFile));
            Platform = platform ?? throw new ArgumentNullException(nameof(platform));
            Arguments = new ReadOnlyDictionary<string, string>(
                arguments?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, string>()
            );
            ExtraSdks = new ReadOnlyCollection<SdkInfo>((extraSdks ?? Enumerable.Empty<SdkInfo>()).ToList());
            ReplayMode = replayMode ?? ReplayMode.RecordCache;
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
                    : null,
                replayMode: obj.TryGetValue("replayMode", out var replayMode)
                    ? Enum.Parse<Pipeline.ReplayMode>((string)replayMode)
                    : (Pipeline.ReplayMode?)null
            ) {}
        
        public string BuildFile { get; }
        
        public override PlatformInfo Platform { get; }
        
        public IReadOnlyDictionary<string, string> Arguments { get; }
        public IReadOnlyList<SdkInfo> ExtraSdks { get; }
        
        public override ReplayMode ReplayMode { get; }
    }

    public sealed class ContainerBuildTask : BuildTaskBase
    {
        public ContainerBuildTask(
            string dockerfile,
            PlatformInfo platform,
            IReadOnlyDictionary<string, string> arguments,
            ReplayMode replayMode
        ) {
            Dockerfile = dockerfile;
            Platform = platform;
            Arguments = arguments;
            ReplayMode = replayMode;
        }

        public string Dockerfile { get; }
        
        public override PlatformInfo Platform { get; }
        
        public IReadOnlyDictionary<string, string> Arguments { get; }
        
        public override ReplayMode ReplayMode { get; }
    }
}