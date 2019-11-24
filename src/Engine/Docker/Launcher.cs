using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Helium.Engine.Docker;
using Helium.JobExecutor;
using Helium.Sdks;
using Newtonsoft.Json;
using static Helium.JobExecutor.JobExecutorProtocol;

namespace Helium.Engine
{
    internal static class Launcher
    {
        public static async Task<int> Run(PlatformInfo platform, LaunchProperties props) {

            var runCommand = BuildRunCommand(platform, props);
            
            var dockerCommand = Environment.GetEnvironmentVariable("HELIUM_DOCKER_COMMAND") ?? "docker";

            var psi = new ProcessStartInfo();
            if(Environment.GetEnvironmentVariable("HELIUM_SUDO_COMMAND") is {} sudoCommand) {
                psi.FileName = sudoCommand;
                psi.ArgumentList.Add(dockerCommand);
            }
            else {
                psi.FileName = dockerCommand;
            }

            switch(Environment.GetEnvironmentVariable("HELIUM_LAUNCH_MODE")) {
                case "docker-cli":
                case null:
                    AddDockerCLIArgs(psi, runCommand);
                    break;
                
                case "job-executor-cli":
                    psi.ArgumentList.Add("run");
                    psi.ArgumentList.Add(JsonConvert.SerializeObject(runCommand));
                    break;
                
                case "job-executor-websocket":
                    throw new NotImplementedException();
                
                default:
                    throw new Exception("Unknown value for HELIUM_LAUNCH_MODE");
            }

            var process = Process.Start(psi) ?? throw new Exception("Could not start docker process.");
            await WaitForExitAsync(process);

            return process.ExitCode;
        }

        private static RunDockerCommand BuildRunCommand(PlatformInfo platform, LaunchProperties props) {
            var rootFSPath = platform.RootDirectory;

            var run = new RunDockerCommand {
                ImageName = props.DockerImage,
                Command = props.Command,
                Environment = new Dictionary<string, string>(props.Environment) {
                    ["HELIUM_SDK_PATH"] = string.Join(Path.PathSeparator, props.PathDirs)
                },
            };
            
            run.BindMounts.Add(new DockerBindMount {
                HostDirectory = Path.GetFullPath(props.SocketDir),
                MountPath = rootFSPath + "helium/socket",
            });

            foreach(var (containerDir, hostDir) in props.SdkDirs) {
                run.BindMounts.Add(new DockerBindMount {
                    HostDirectory = Path.GetFullPath(hostDir),
                    MountPath = containerDir,
                    IsReadOnly = true,
                });
            }

            run.BindMounts.Add(new DockerBindMount {
                HostDirectory = Path.GetFullPath(props.Sources),
                MountPath = rootFSPath + "sources",
            });

            run.BindMounts.Add(new DockerBindMount {
                HostDirectory = Path.GetFullPath(props.InstallDir),
                MountPath = rootFSPath + "helium/install",
            });

            return run;
        }

        private static void AddDockerCLIArgs(ProcessStartInfo psi, RunDockerCommand run) {
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

            foreach(var bindMount in run.BindMounts) {
                var mountSpec = bindMount.HostDirectory + ":" + bindMount.MountPath;
                if(bindMount.IsReadOnly) mountSpec += ":ro";
                
                psi.ArgumentList.Add("-v");
                psi.ArgumentList.Add(mountSpec);
            }

            foreach(var (name, value) in run.Environment) {
                psi.ArgumentList.Add("-e");
                psi.ArgumentList.Add($"{name}={value}");
            }

            psi.ArgumentList.Add(run.ImageName);

            run.Command.ForEach(psi.ArgumentList.Add);
        }

        private static Task WaitForExitAsync(Process process) {
            var tcs = new TaskCompletionSource<object?>();

            process.EnableRaisingEvents = true;
            process.Exited += delegate { tcs.TrySetResult(null); };

            return tcs.Task;
        }
        
    }
}