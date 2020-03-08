using System.IO;
using System.Threading.Tasks;
using FSharp.Control.Tasks;
using Helium.Sdks;

namespace Helium.Engine.ContainerBuild
{
    public class ContainerBuildManager
    {
        public static async Task<int> Dummy(string dockerfilePath, PlatformInfo platform) {

            using var reader = File.OpenText(dockerfilePath);
            var newDockerfile = await DockerfileResolver.ProcessDockerfile(reader, platform);
            
            return 0;
        } 
    }
}