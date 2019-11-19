using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Helium.Engine.Docker;
using Helium.Sdks;

namespace Helium.Engine
{
    internal static class Launcher
    {
        public static async Task<int> Run(PlatformInfo platform, LaunchProperties props) {
            var rootFSPath = platform.RootDirectory;

            var psi = new ProcessStartInfo();

            var dockerCommand = Environment.GetEnvironmentVariable("HELIUM_DOCKER_COMMAND") ?? "docker";
            if(Environment.GetEnvironmentVariable("HELIUM_SUDO_COMMAND") is {} sudoCommand) {
                psi.FileName = sudoCommand;
                psi.ArgumentList.Add(dockerCommand);
            }
            else {
                psi.FileName = dockerCommand;
            }
            
            psi.ArgumentList.Add("run");
            psi.ArgumentList.Add("--rm");
            
            psi.ArgumentList.Add("--network");
            psi.ArgumentList.Add("none");

            psi.ArgumentList.Add("--hostname");
            psi.ArgumentList.Add("helium-build-env");

            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                psi.ArgumentList.Add("--isolation");
                psi.ArgumentList.Add("process");
            }
            
            psi.ArgumentList.Add("-v");
            psi.ArgumentList.Add($"{Path.GetFullPath(props.SocketDir)}:{rootFSPath}helium/socket");

            foreach(var (containerDir, hostDir) in props.SdkDirs) {
                psi.ArgumentList.Add("-v");
                psi.ArgumentList.Add($"{Path.GetFullPath(hostDir)}:{containerDir}:ro");
            }

            var env = new Dictionary<string, string>(props.Environment);
            env["HELIUM_SDK_PATH"] = string.Join(Path.PathSeparator, props.PathDirs);

            foreach(var (name, value) in env) {
                psi.ArgumentList.Add("-e");
                psi.ArgumentList.Add($"{name}={value}");
            }

            psi.ArgumentList.Add("-v");
            psi.ArgumentList.Add($"{Path.GetFullPath(props.Sources)}:{rootFSPath}sources");

            psi.ArgumentList.Add("-v");
            psi.ArgumentList.Add($"{Path.GetFullPath(props.InstallDir)}:{rootFSPath}helium/install");
            
            psi.ArgumentList.Add(props.DockerImage);
            
            props.Command.ForEach(psi.ArgumentList.Add);

            var process = Process.Start(psi) ?? throw new Exception("Could not start docker process.");
            await WaitForExitAsync(process);

            return process.ExitCode;
        }

        private static Task WaitForExitAsync(Process process) {
            var tcs = new TaskCompletionSource<object?>();

            process.EnableRaisingEvents = true;
            process.Exited += delegate { tcs.TrySetResult(null); };

            return tcs.Task;
        }
        
    }
}