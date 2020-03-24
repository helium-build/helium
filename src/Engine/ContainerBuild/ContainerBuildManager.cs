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
        public static async Task<int> Run(ILauncher launcher, IRecorder recorder) {

            var cacheDir = await recorder.GetCacheDir();
            var buildContextFile = await recorder.GetBuildContext();

            var props = new ContainerBuildProperties(
                new Dictionary<string, string>(),
                recorder.ImageFile,
                cacheDir,
                buildContextFile
            );

            return await launcher.BuildContainer(recorder.Platform, props);
        } 
    }
}