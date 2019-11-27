using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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
                    return await RunWebSocketExecutor(runCommand, default(CancellationToken));
                
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

        private static async Task<int> RunWebSocketExecutor(RunDockerCommand runCommand, CancellationToken cancellationToken) {
            var uri = Environment.GetEnvironmentVariable("HELIUM_JOB_EXECUTOR_URL");
            if(uri == null) {
                throw new Exception("Variable HELIUM_JOB_EXECUTOR_URL not specified.");
            }

            var ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri(uri), cancellationToken);

            var startMessage = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(runCommand));
            await ws.SendAsync(new ArraySegment<byte>(startMessage), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);

            using var stdout = Console.OpenStandardOutput();
            using var stderr = Console.OpenStandardError();
            var buffer = new byte[4096];

            async Task HandleOutput(Stream stream) {
                while(!cancellationToken.IsCancellationRequested) {
                    var msg = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    if(msg.MessageType == WebSocketMessageType.Close) break;
                    if(msg.MessageType == WebSocketMessageType.Text) continue;
                    await stream.WriteAsync(buffer, 0, msg.Count);
                }
            }

            while(!cancellationToken.IsCancellationRequested) {
                var msg = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if(msg.MessageType == WebSocketMessageType.Binary) {
                    if(msg.Count > 0) {
                        switch(buffer[0]) {
                            case 0x00:
                                await stdout.WriteAsync(buffer, 1, msg.Count - 1);
                                if(!msg.EndOfMessage) await HandleOutput(stdout);
                                break;

                            case 0x01:
                                await stderr.WriteAsync(buffer, 1, msg.Count - 1);
                                if(!msg.EndOfMessage) await HandleOutput(stderr);
                                break;


                        }
                    }
                }
                else {
                    string json;
                    if(msg.EndOfMessage) {
                        json = Encoding.UTF8.GetString(buffer, 0, msg.Count);
                    }
                    else {
                        var memoryStream = new MemoryStream();

                        memoryStream.Write(buffer, 0, msg.Count);

                        while(!cancellationToken.IsCancellationRequested && !msg.EndOfMessage) {
                            msg = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                            if(msg.MessageType == WebSocketMessageType.Close) break;
                            if(msg.MessageType == WebSocketMessageType.Binary) continue;
                            memoryStream.Write(buffer, 0, msg.Count);
                        }

                        json = Encoding.UTF8.GetString(memoryStream.ToArray());
                    }

                    var exitCode = JsonConvert.DeserializeObject<RunDockerExitCode>(json);
                    
                    return exitCode.ExitCode;
                }
            }

            throw new OperationCanceledException();
        }

        private static Task WaitForExitAsync(Process process) {
            var tcs = new TaskCompletionSource<object?>();

            process.EnableRaisingEvents = true;
            process.Exited += delegate { tcs.TrySetResult(null); };

            return tcs.Task;
        }
        
    }
}