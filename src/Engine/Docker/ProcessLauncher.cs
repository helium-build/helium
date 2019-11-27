using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Helium.Engine.Docker;
using Helium.Sdks;
using static Helium.JobExecutor.JobExecutorProtocol;

namespace Engine.Docker
{
    internal abstract class ProcessLauncher : LauncherBase
    {
        public ProcessLauncher(string? sudoCommand, string dockerCommand) {
            this.sudoCommand = sudoCommand;
            this.dockerCommand = dockerCommand;
        }

        private readonly string? sudoCommand;
        private readonly string dockerCommand;
        
        
        public sealed override async Task<int> Run(PlatformInfo platform, LaunchProperties props) {
            var runCommand = BuildRunCommand(platform, props);

            var psi = new ProcessStartInfo();
            
            if(sudoCommand != null) {
                psi.FileName = sudoCommand;
                psi.ArgumentList.Add(dockerCommand);
            }
            else {
                psi.FileName = dockerCommand;
            }
            
            AddArguments(psi, runCommand);

            var process = Process.Start(psi) ?? throw new Exception("Could not start docker process.");
            await WaitForExitAsync(process);

            return process.ExitCode;
        }

        protected abstract void AddArguments(ProcessStartInfo psi, RunDockerCommand run);
        
        
        private static Task WaitForExitAsync(Process process) {
            var tcs = new TaskCompletionSource<object?>();

            process.EnableRaisingEvents = true;
            process.Exited += delegate { tcs.TrySetResult(null); };

            return tcs.Task;
        }
    }
}