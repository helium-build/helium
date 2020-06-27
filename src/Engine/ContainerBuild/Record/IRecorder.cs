using System.Collections.Generic;
using System.Threading.Tasks;
using Helium.DockerfileHandler;
using Helium.Sdks;

namespace Helium.Engine.ContainerBuild
{
    public interface IRecorder
    {
        PlatformInfo Platform { get; }
        
        IReadOnlyDictionary<string, string> BuildArgs { get; }
        
        string WorkspaceDir { get; }

        Task<string> GetCacheDir();
        bool EnableNetwork { get; }
        
        string ImageFile { get; }
        
        Task<DockerfileInfo> LoadDockerfile();


        string BuildContext { get; }

        Task CompleteBuild();
    }
}