using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Helium.Engine.Docker;
using Helium.Sdks;

namespace Helium.Engine.ContainerBuild
{
    internal static class ContainerBuildProgram
    {
        public static async Task<int> ContainerBuildMain(Program.ContainerBuild options) {

            var launcher = Launcher.DetectLauncher();
            
            var platform = new PlatformInfo(
                os: options.OperatingSystem ?? throw new Exception("Invalid OS."),
                arch: options.Architecture ?? throw new Exception("Invalid Architecture")
            );

            if(options.Workspace == null) {
                throw new Exception("Workspace is missing.");
            }

            if(options.OutputFile == null) {
                throw new Exception("Output file is missing.");
            }

            string buildContext = options.BuildContext ?? Path.Combine(options.Workspace, "build-context");
            
            var dockerfilePath = options.DockerfilePath ?? Path.Combine(buildContext, "Dockerfile");

            IRecorder recorder;
            if(options.Archive == null) {
                recorder = new NullRecorder(platform, options.Workspace, buildContext,dockerfilePath, options.OutputFile, new Dictionary<string, string>());
            }
            else {
                throw new NotImplementedException();
            }
            
            return await ContainerBuildManager.Run(launcher, recorder);
        }
    }
}