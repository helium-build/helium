using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Helium.Sdks
{
    public class SdkInfo
    {
        public SdkInfo(
            IEnumerable<string> implements,
            string version,
            IEnumerable<PlatformInfo> platforms,
            IEnumerable<SdkSetupStep> setupSteps,
            IEnumerable<string>? pathDirs = null,
            IReadOnlyDictionary<string, EnvValue>? env = null,
            IReadOnlyDictionary<string, string>? configFileTemplates = null
        ) {
            Implements = implements.ToList().AsReadOnly();
            Version = version ?? throw new ArgumentNullException(nameof(version));
            Platforms = platforms.ToList().AsReadOnly();
            SetupSteps = setupSteps.ToList().AsReadOnly();
            PathDirs = (pathDirs ?? Enumerable.Empty<string>()).ToList().AsReadOnly();
            Env = env?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, EnvValue>();
            ConfigFileTemplates = configFileTemplates?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, string>();
        }

        public IReadOnlyList<string> Implements { get; }
        public string Version { get; }
        public IReadOnlyList<PlatformInfo> Platforms { get; }
        public IReadOnlyList<SdkSetupStep> SetupSteps { get; }
        public IReadOnlyList<string> PathDirs { get; }
        public IReadOnlyDictionary<string, EnvValue> Env { get; }
        public IReadOnlyDictionary<string, string> ConfigFileTemplates { get; }

        public bool Matches(string name, string versionRange) =>
            Implements.Contains(name) && new SemVer.Range(versionRange).IsSatisfied(Version);

        public bool SupportedBy(PlatformInfo platform) =>
            Platforms.Any(platform.SupportsRunning);

    }
}