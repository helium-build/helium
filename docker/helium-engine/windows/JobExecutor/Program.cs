using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
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

            public string ImageName { get; set; }

            public List<string> Command { get; set; } = new List<string>();
        }

        sealed class DockerBindMount {
            public string HostDirectory { get; set; }
            public string MountPath { get; set; }
            public bool IsReadOnly { get; set; }
        }

        sealed class RunDockerExitCode {
            public int ExitCode { get; set; }
        }

        static void Main(string[] args)
        {
            allowedMounts = new[] {
                new AllowedMount {
                    BasePath = @"C:\workspace\",
                    RealPath = Environment.GetEnvironmentVariable("HELIUM_REALPATH_WORKSPACE"),
                },
                new AllowedMount {
                    BasePath = @"C:\cache\",
                    RealPath = Environment.GetEnvironmentVariable("HELIUM_REALPATH_CACHE"),
                },
            };

            var cancel = new CancellationTokenSource();

            using(var server = new WebSocketServer("ws://0.0.0.0:8181")) {
                server.Start(socket => {
                    var lockObj = new object();
                    var state = SocketState.Waiting;

                    socket.OnMessage = message => {
                        var commandObj = JsonConvert.DeserializeObject<Command>(message);
                        lock(lockObj) {
                            switch((state, commandObj)) {
                                case (SocketState.Waiting, RunDockerCommand command):
                                    state = SocketState.Running;
                                    Task.Run(() => RunDocker(command, socket, cancel.Token));
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
        }

        static async Task RunDocker(RunDockerCommand command, IWebSocketConnection conn, CancellationToken cancellationToken) {
            if(!ValidateDockerCommand(command)) {
                await conn.Send(JsonConvert.SerializeObject(new RunDockerExitCode { ExitCode = 1 }));
                return;
            }

            try {
                var client = new DockerClientConfiguration(new Uri("npipe://./pipe/docker_engine")).CreateClient();

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
                            Isolation = "process",
                            
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

                using(var stream = await client.Containers.AttachContainerAsync(
                    response.ID,
                    tty: false,
                    new Docker.DotNet.Models.ContainerAttachParameters {
                        Stream = true,
                        Stderr = true,
                        Stdout = true,
                    },
                    cancellationToken
                )) {
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
                            {
                                byte[] b2 = new byte[1 + result.Count];
                                b2[0] = 0;
                                Array.Copy(buffer, 0, b2, 1, result.Count);
                                await conn.Send(b2);
                            }
                                break;

                            case MultiplexedStream.TargetStream.StandardError:
                            {
                                byte[] b2 = new byte[1 + result.Count];
                                b2[0] = 1;
                                Array.Copy(buffer, 0, b2, 1, result.Count);
                                await conn.Send(b2);
                            }
                                break;
                        }
                    }
                }
            }
            catch {
                await conn.Send(JsonConvert.SerializeObject(new RunDockerExitCode { ExitCode = 1 }));
                throw;
            }
            
            await conn.Send(JsonConvert.SerializeObject(new RunDockerExitCode { ExitCode = 0 }));
        }

        private static bool ValidateDockerCommand(RunDockerCommand command)
        {
            if(!Regex.IsMatch(command.ImageName, @"^helium-build/build-env\:[a-z0-9\-]+$")) {
                return false;
            }

            foreach(var mount in command.BindMounts) {
                foreach(var allowedMount in allowedMounts) {
                    if(mount.HostDirectory.StartsWith(allowedMount.BasePath)) {
                        mount.HostDirectory = allowedMount.RealPath + mount.HostDirectory.Substring(allowedMount.BasePath.Length);
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
            public string BasePath { get; set; }
            public string RealPath { get; set; }
        }

        private static AllowedMount[] allowedMounts;
    }
}
