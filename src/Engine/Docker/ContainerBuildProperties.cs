using System.Collections.Generic;

namespace Helium.Engine.Docker
{
    public sealed class ContainerBuildProperties
    {
        public ContainerBuildProperties(IReadOnlyDictionary<string, string> buildArgs, string outputFile, string cacheDir, string workspaceTar) {
            BuildArgs = buildArgs;
            OutputFile = outputFile;
            CacheDir = cacheDir;
            WorkspaceTar = workspaceTar;
        }

        public IReadOnlyDictionary<string, string> BuildArgs { get; }
        public string OutputFile { get; }
        public string CacheDir { get; }
        public string WorkspaceTar { get; }
    }
}