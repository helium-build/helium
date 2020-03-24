using System.Collections.Generic;

namespace Helium.Engine.Docker
{
    public sealed class ContainerBuildProperties
    {
        public ContainerBuildProperties(IReadOnlyDictionary<string, string> buildArgs, string outputFile, string cacheDir, string buildContextArchive, bool enableProxyNetwork) {
            BuildArgs = buildArgs;
            OutputFile = outputFile;
            CacheDir = cacheDir;
            BuildContextArchive = buildContextArchive;
            EnableProxyNetwork = enableProxyNetwork;
        }

        public IReadOnlyDictionary<string, string> BuildArgs { get; }
        public string OutputFile { get; }
        public string CacheDir { get; }
        public string BuildContextArchive { get; }
        public bool EnableProxyNetwork { get; }
    }
}