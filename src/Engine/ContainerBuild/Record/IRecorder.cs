using System.Threading.Tasks;
using Helium.Sdks;

namespace Helium.Engine.ContainerBuild
{
    public interface IRecorder
    {
        PlatformInfo Platform { get; }
        
        Task<string> GetCacheDir();
        bool EnableNetwork { get; }

        Task<string> GetBuildContext();
        
        string ImageFile { get; }

        Task CompleteBuild();
    }
}