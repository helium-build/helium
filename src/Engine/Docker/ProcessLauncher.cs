using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Helium.Engine.Docker;
using Helium.Sdks;
using Helium.Util;
using Helium.Engine.BuildExecutor.Protocol;

namespace Helium.Engine.Docker
{
    internal abstract class ProcessLauncher : LauncherBase
    {
        public ProcessLauncher(string? sudoCommand, string dockerCommand) {
            this.sudoCommand = sudoCommand;
            this.dockerCommand = dockerCommand;
        }

        private readonly string? sudoCommand;
        private readonly string dockerCommand;

        protected ProcessStartInfo CreatePSI() {
            var psi = new ProcessStartInfo();
            
            if(sudoCommand != null) {
                psi.FileName = sudoCommand;
                psi.ArgumentList.Add(dockerCommand);
            }
            else {
                psi.FileName = dockerCommand;
            }

            return psi;
        }
        
        public sealed override async Task<int> Run(PlatformInfo platform, LaunchProperties props) {
            var runCommand = BuildRunCommand(platform, props);

            var psi = CreatePSI();
            
            AddRunArguments(psi, runCommand);

            var process = Process.Start(psi) ?? throw new Exception("Could not start docker process.");
            await process.WaitForExitAsync();

            return process.ExitCode;
        }

        protected abstract void AddRunArguments(ProcessStartInfo psi, RunDockerCommand run);

        public sealed override async Task<int> BuildContainer(PlatformInfo platform, RunDockerBuild props) {
            var psi = CreatePSI();
            
            AddContainerBuildArguments(psi, props);

            var process = Process.Start(psi) ?? throw new Exception("Could not start docker process.");
            await process.WaitForExitAsync();

            return process.ExitCode;
        }

        protected abstract void AddContainerBuildArguments(ProcessStartInfo psi, RunDockerBuild build);
    }
}