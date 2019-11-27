using System.Threading.Tasks;
using Helium.Engine.Docker;
using Helium.Sdks;

namespace Engine.Docker
{
    internal interface ILauncher
    {
        Task<int> Run(PlatformInfo platform, LaunchProperties props);
    }
}