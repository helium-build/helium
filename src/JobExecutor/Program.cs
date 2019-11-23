using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using Fleck;
using Newtonsoft.Json;

namespace JobExecutor
{
    class Program
    {
        enum SocketState {
            Waiting,
            Running,
        }

        abstract class Command {}

        [DisplayName("stop")]
        sealed class StopCommand : Command {
            public bool Stop { get; set; }
        }

        [DisplayName("run-docker")]
        sealed class RunDockerCommand : Command {
            public Dictionary<string, string> Environment { get; set; } = new Dictionary<string, string>();

            public List<DockerBindMount> BindMounts { get; set; } = new List<DockerBindMount>();

            public string? ImageName { get; set; }

            public List<string> Command { get; set; } = new List<string>();
        }

        sealed class DockerBindMount {
            public string? HostDirectory { get; set; }
            public string? MountPath { get; set; }
            public bool IsReadOnly { get; set; }
        }

        sealed class RunDockerExitCode {
            public int ExitCode { get; set; }
        }

        static int Main(string[] args)
        {
            if(args.Length == 0) {
                Console.WriteLine("Invalid arguments.");
                return 1;
            }

            var cancel = new CancellationTokenSource();

            var client = CreateDockerClient();

            switch(args[0]) {
                case "run":
                    if(args.Length != 2) {
                        Console.WriteLine("Invalid arguments.");
                        return 1;
                    }

                    return RunConsole(client, args[1], cancel.Token).Result;
                    
                case "serve":
                    ServeWSApi(client, cancel);
                    return 0;
                
                default:
                    Console.WriteLine($"Unknown command: {args[0]}");
                    return 1;
            }

        }

        private static async Task<int> RunConsole(IDockerClient client, string commandJson, CancellationToken cancellationToken) {
            var command = JsonConvert.DeserializeObject<RunDockerCommand>(commandJson);
            var containerId = await LookupCurrentContainerId(client, cancellationToken);
            return await RunDocker(client, command, containerId, new ConsoleOutputObserver(), cancellationToken);
        }

        private static void ServeWSApi(IDockerClient client, CancellationTokenSource cancel) {
            using var server = new WebSocketServer("ws://0.0.0.0:8181");
            server.Start(socket => {
                var lockObj = new object();
                var state = SocketState.Waiting;

                socket.OnMessage = message => {
                    var commandObj = JsonConvert.DeserializeObject<Command>(message);
                    lock(lockObj) {
                        switch((state, commandObj)) {
                            case (SocketState.Waiting, RunDockerCommand command):
                                state = SocketState.Running;
                                Task.Run(async () => {
                                    var containerId = await LookupContainerIdFromIpAddress(client, new[] { socket.ConnectionInfo.ClientIpAddress }, cancel.Token);
                                    return await RunDocker(client, command, containerId, new WebSocketOutputObserver(socket), cancel.Token);
                                });
                                break;

                            case (_, StopCommand command) when command.Stop:
                                cancel.Cancel();
                                break;
                        }
                    }
                };
            });

            cancel.Token.WaitHandle.WaitOne();
        }

        static DockerClient CreateDockerClient() {
            var path = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "npipe://./pipe/docker_engine"
                : "/var/run/docker.sock:/var/run/docker.sock";
            
            return new DockerClientConfiguration(new Uri(path)).CreateClient();
        }
        
        static async Task<IReadOnlyList<AllowedMount>> GetMounts(IDockerClient client, string containerId, CancellationToken cancellationToken) {
            var info = await client.Containers.InspectContainerAsync(containerId, cancellationToken);

            return info.Mounts.Select(mount => new AllowedMount {
                BasePath = mount.Destination,
                RealPath = mount.Source,
                ReadWrite = mount.RW,
            }).ToList();
        }

        private static async Task<int> RunDocker(IDockerClient client, RunDockerCommand command, string callingContainerId, IOutputObserver outputObserver, CancellationToken cancellationToken) {
            var allowedMounts = await GetMounts(client, callingContainerId, cancellationToken);
            
            if(!ValidateDockerCommand(command, allowedMounts)) {
                return 1;
            }

            var response = await client.Containers.CreateContainerAsync(
                new Docker.DotNet.Models.CreateContainerParameters {
                    Image = command.ImageName,
                    Env = command.Environment.Select(env => $"{env.Key}={env.Value}").ToList(),
                    AttachStderr = true,
                    AttachStdout = true,
                    ArgsEscaped = false,
                    Cmd = command.Command,

                    HostConfig = new Docker.DotNet.Models.HostConfig {
                        AutoRemove = true,
                        NetworkMode = "none",
                        Isolation = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "process" : null,
                        
                        Mounts = command.BindMounts.Select(bindMount => new Docker.DotNet.Models.Mount {
                            Type = "bind",
                            Source = bindMount.HostDirectory,
                            Target = bindMount.MountPath,
                            ReadOnly = bindMount.IsReadOnly
                        }).ToList(),
                    },
                },
                cancellationToken
            );

            using var stream = await client.Containers.AttachContainerAsync(
                response.ID,
                tty: false,
                new Docker.DotNet.Models.ContainerAttachParameters {
                    Stream = true,
                    Stderr = true,
                    Stdout = true,
                },
                cancellationToken
            );
            
            await client.Containers.StartContainerAsync(
                response.ID,
                new Docker.DotNet.Models.ContainerStartParameters {},
                cancellationToken
            );

            byte[] buffer = new byte[1024];
            while(!cancellationToken.IsCancellationRequested) {
                var result = await stream.ReadOutputAsync(buffer, 0, buffer.Length, cancellationToken);
                    
                if(result.EOF) {
                    break;
                }

                switch(result.Target) {
                    case MultiplexedStream.TargetStream.StandardOut:
                        await outputObserver.StandardOutput(buffer, result.Count);
                        break;

                    case MultiplexedStream.TargetStream.StandardError:
                        await outputObserver.StandardError(buffer, result.Count);
                        break;
                }
            }

            var waitResponse = await client.Containers.WaitContainerAsync(response.ID, cancellationToken);
            return (int)waitResponse.StatusCode;
        }

        private static bool ValidateDockerCommand(RunDockerCommand command, IReadOnlyList<AllowedMount> allowedMounts)
        {
            if(!Regex.IsMatch(command.ImageName, @"^helium-build/build-env\:[a-z0-9\-]+$")) {
                return false;
            }

            foreach(var mount in command.BindMounts) {
                if(mount.HostDirectory == null || mount.MountPath == null) {
                    return false;
                }
                
                foreach(var allowedMount in allowedMounts) {
                    if(allowedMount.BasePath == null || allowedMount.RealPath == null) {
                        continue;
                    }
                    
                    if(mount.HostDirectory.StartsWith(allowedMount.BasePath)) {
                        mount.HostDirectory = allowedMount.RealPath + mount.HostDirectory.Substring(allowedMount.BasePath.Length);
                        mount.IsReadOnly &= !allowedMount.ReadWrite;
                        goto validMount;
                    }
                }

                return false;

            validMount:
                continue;
            }

            return true;
        }

        sealed class AllowedMount {
            public string? BasePath { get; set; }
            public string? RealPath { get; set; }
            
            public bool ReadWrite { get; set; }
        }

        private static async Task<string> LookupContainerIdFromIpAddress(IDockerClient client, string[] ip, CancellationToken cancellationToken) {
            var containers = await client.Containers.ListContainersAsync(new ContainersListParameters(), cancellationToken);
            foreach(var container in containers) {
                foreach(var (_, network) in container.NetworkSettings.Networks) {
                    if(ip.Contains(network.IPAddress)) return container.ID;
                }
            }
            
            throw new Exception("Could not find caller's container id.");
        }

        private static async Task<string> LookupCurrentContainerId(IDockerClient client, CancellationToken cancellationToken) {
            var ip = (await Dns.GetHostAddressesAsync(Dns.GetHostName())).Select(addr => addr.ToString()).ToArray();
            return await LookupContainerIdFromIpAddress(client, ip, cancellationToken);
        }
        
        
    }
}
