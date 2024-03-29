﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
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
using Helium.Engine.BuildExecutor.Protocol;

namespace Helium.Engine.BuildExecutor
{
    internal static class Program
    {

        private static async Task<int> Main(string[] args)
        {
            if(args.Length == 0) {
                Console.Error.WriteLine("Invalid arguments.");
                return 1;
            }

            var cancel = new CancellationTokenSource();

            var client = CreateDockerClient();

            switch(args[0]) {
                case "run":
                    if(args.Length != 2) {
                        Console.Error.WriteLine("Invalid arguments.");
                        return 1;
                    }

                    return await RunConsole(client, args[1], cancel.Token);

                case "container-build":
                {
                    if(args.Length != 2) {
                        Console.Error.WriteLine("Invalid arguments.");
                        return 1;
                    }
                    
                    var buildJob = new ContainerBuildJob(client);

                    return await buildJob.RunBuild( JsonConvert.DeserializeObject<RunDockerBuild>(args[1]), new ConsoleOutputObserver(), cancel.Token);
                }

                case "serve":
                    ServeWSApi(client, cancel);
                    return 0;
                
                default:
                    Console.Error.WriteLine($"Unknown command: {args[0]}");
                    return 1;
            }

        }

        private static async Task<int> RunConsole(IDockerClient client, string commandJson, CancellationToken cancellationToken) {
            var command = JsonConvert.DeserializeObject<RunDockerCommand>(commandJson);
            var containerId = await LookupCurrentContainerId(client, cancellationToken);
            if(containerId == null) {
                Console.WriteLine("Could not obtain current container id.");
                return 1;
            }
            
            return await RunDocker(client, command, containerId, new ConsoleOutputObserver(), cancellationToken);
        }

        private static void ServeWSApi(IDockerClient client, CancellationTokenSource cancel) {
            using var server = new WebSocketServer("ws://0.0.0.0:8181");
            server.Start(socket => {
                var lockObj = new object();
                var state = SocketState.Waiting;

                socket.OnMessage = message => {
                    var commandObj = JsonConvert.DeserializeObject<CommandBase>(message);
                    lock(lockObj) {
                        switch((state, commandObj)) {
                            case (SocketState.Waiting, RunDockerCommand command):
                                state = SocketState.Running;
                                Task.Run(async () => {
                                    var containerId = await LookupContainerIdFromIpAddress(client, new[] { socket.ConnectionInfo.ClientIpAddress }, cancel.Token);
                                    if(containerId == null) {
                                        throw new Exception("Could not find caller's container id.");
                                    }
                                    
                                    int exitCode;
                                    try {
                                        exitCode = await RunDocker(client, command, containerId, new WebSocketOutputObserver(socket), cancel.Token);
                                    }
                                    catch {
                                        await socket.Send(JsonConvert.SerializeObject(new RunDockerExitCode { ExitCode = 1 }));
                                        throw;
                                    }
                                    await socket.Send(JsonConvert.SerializeObject(new RunDockerExitCode { ExitCode = exitCode }));
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
                : "unix:///var/run/docker.sock";
            
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
            
            if(!(ValidateDockerCommand(command) && ValidateMounts(command.BindMounts, allowedMounts) is {} mounts)) {
                await Console.Error.WriteLineAsync("Could not validate docker command.");
                return 1;
            }
            
            

            var response = await client.Containers.CreateContainerAsync(
                new Docker.DotNet.Models.CreateContainerParameters {
                    Image = command.ImageName,
                    Env = command.Environment.Select(env => $"{env.Key}={env.Value}").ToList(),
                    AttachStderr = true,
                    AttachStdout = true,
                    ArgsEscaped = false,
                    Cmd = command.Command.ToList(),
                    WorkingDir = command.CurrentDirectory,

                    HostConfig = new Docker.DotNet.Models.HostConfig {
                        AutoRemove = true,
                        NetworkMode = "none",
                        Isolation = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "process" : null,
                        
                        Mounts = mounts,
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

            await DockerHelpers.PipeContainerOutput(outputObserver, stream, cancellationToken);

            var waitResponse = await client.Containers.WaitContainerAsync(response.ID, cancellationToken);
            return (int)waitResponse.StatusCode;
        }

        private static bool ValidateDockerCommand(RunDockerCommand command) {
            if(command.ImageName == null || !Regex.IsMatch(command.ImageName, @"^helium-build/build-env\:[a-z0-9\-]+$")) {
                return false;
            }

            return true;
        }
        
        private static List<Docker.DotNet.Models.Mount>? ValidateMounts(IEnumerable<DockerBindMount> bindMounts, IReadOnlyList<AllowedMount> allowedMounts) {
            var mounts = new List<Docker.DotNet.Models.Mount>();

            foreach(var mount in bindMounts) {
                if(mount.HostDirectory == null || mount.MountPath == null) {
                    return null;
                }
                
                foreach(var allowedMount in allowedMounts) {
                    if(allowedMount.BasePath == null || allowedMount.RealPath == null) {
                        continue;
                    }

                    bool isWin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

                    bool HasTrailingSlash(string path) =>
                        path.EndsWith("/") || (isWin && path.EndsWith("\\"));
                    
                    bool SuffixStartsWithSlash(string prefix, string path) =>
                        path.Length > prefix.Length &&
                            path[prefix.Length] is var slash &&
                            (slash == '/' || (isWin && slash == '\\'));

                    if(!mount.HostDirectory.StartsWith(allowedMount.BasePath)) continue;

                    var suffixHasSlash = SuffixStartsWithSlash(allowedMount.BasePath, mount.HostDirectory);
                    
                    if(!HasTrailingSlash(allowedMount.BasePath) && !suffixHasSlash) {
                        continue;
                    }
                    
                    var subPath = mount.HostDirectory.Substring(allowedMount.BasePath.Length + (suffixHasSlash ? 1 : 0));
                    
                    var sep = HasTrailingSlash(allowedMount.RealPath) ? "" : Path.DirectorySeparatorChar.ToString();
                    mounts.Add(new Docker.DotNet.Models.Mount {
                        Type = "bind",
                        Source = allowedMount.RealPath + sep + subPath,
                        Target = mount.MountPath,
                        ReadOnly = mount.IsReadOnly || !allowedMount.ReadWrite,
                    });
                    goto validMount;
                }

                return null;

            validMount:
                continue;
            }

            return mounts;
        }

        sealed class AllowedMount {
            public string? BasePath { get; set; }
            public string? RealPath { get; set; }
            
            public bool ReadWrite { get; set; }
        }

        private static async Task<string?> LookupContainerIdFromIpAddress(IDockerClient client, string[] ip, CancellationToken cancellationToken) {
            var containers = await client.Containers.ListContainersAsync(new ContainersListParameters(), cancellationToken);
            foreach(var container in containers) {
                foreach(var (_, network) in container.NetworkSettings.Networks) {
                    if(ip.Contains(network.IPAddress)) return container.ID;
                }
            }

            return null;
        }

        private static async Task<string?> LookupCurrentContainerId(IDockerClient client, CancellationToken cancellationToken) {
            var ip = (await Dns.GetHostAddressesAsync(Dns.GetHostName())).Select(addr => addr.ToString()).ToArray();
            return await LookupContainerIdFromIpAddress(client, ip, cancellationToken);
        }
        
        
    }
}
