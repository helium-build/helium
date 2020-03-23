using System;
using System.IO;
using System.Threading.Tasks;
using Helium.Engine.Docker;
using Helium.Sdks;

namespace Helium.Engine.Docker
{
    internal interface ILauncher
    {
        Task<int> Run(PlatformInfo platform, LaunchProperties props);

        Task<int> BuildContainer(PlatformInfo platform, ContainerBuildProperties props);
    }
}