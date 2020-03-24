using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FSharp.Control.Tasks;
using Helium.Engine.Docker;
using Helium.Sdks;
using Helium.Util;

namespace Helium.Engine.ContainerBuild
{
    internal static class ContainerBuildManager
    {
        public static async Task<int> Run(ILauncher launcher, string workspace, string buildContext, string dockerfilePath, string outputFile, PlatformInfo platform) {

            string newDockerfile;
            using(var reader = File.OpenText(dockerfilePath)) {
                (newDockerfile, _) = await DockerfileResolver.ProcessDockerfile(reader, platform);
            }
            
            string buildContextFile;
            await using(var buildContextStream = FileUtil.CreateTempFile(workspace, out buildContextFile)) {
                await BuildContextHandler.WriteBuildContext(buildContext, newDockerfile, buildContextStream);
            }

            var cacheDir = DirectoryUtil.CreateTempDirectory(workspace);

            var props = new ContainerBuildProperties(
                new Dictionary<string, string>(),
                outputFile,
                cacheDir,
                buildContextFile
            );

            return await launcher.BuildContainer(platform, props);
        } 
    }
}