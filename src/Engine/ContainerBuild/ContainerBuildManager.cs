using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Helium.Engine.BuildExecutor.Protocol;
using Helium.Engine.Docker;
using Helium.Sdks;
using Helium.Util;

namespace Helium.Engine.ContainerBuild
{
    internal static class ContainerBuildManager
    {
        public static async Task<int> Run(ILauncher launcher, IRecorder recorder) {

            var cacheDir = await recorder.GetCacheDir();

            var dockerfile = await recorder.LoadDockerfile();

            string tempImageFile;
            await using(FileUtil.CreateTempFile(recorder.WorkspaceDir, out tempImageFile)) {}

            var props = new RunDockerBuild(
                buildContextDir: recorder.BuildContext,
                cacheDir: cacheDir,
                enableNetwork: recorder.EnableNetwork,
                dockerfile: dockerfile,
                proxyImage: "helium-build/container-build-proxy:debian-buster-20190708",
                outputFile: tempImageFile,
                buildArgs: recorder.BuildArgs
            );

            return await launcher.BuildContainer(recorder.Platform, props);
        } 
    }
}