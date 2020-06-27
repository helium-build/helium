using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using Helium.DockerfileHandler;
using Helium.DockerfileHandler.Commands;
using Helium.Engine.BuildExecutor.Protocol;
using Helium.Util;

namespace Helium.Engine.BuildExecutor
{
    internal sealed class ContainerBuildJob
    {
        private const string networkTag = "helium-container-build-proxy";
        private const string buildProxyHostname = "helium-container-build-proxy";
        private const string proxySpec = "http://" + buildProxyHostname + ":8000";
        private readonly List<string> proxyEnv = new List<string> {
            "HTTP_PROXY=" + proxySpec,
            "http_proxy=" + proxySpec,
            "HTTPS_PROXY=" + proxySpec,
            "https_proxy=" + proxySpec,
            "FTP_PROXY=" + proxySpec,
            "ftp_proxy=" + proxySpec,
        };
        
        public ContainerBuildJob(IDockerClient docker) {
            this.docker = docker;
        }
     
        
        private readonly IDockerClient docker;
        
        
        public class BuildState
        {
            public BuildState(string id) {
                Id = id;
            }
            
            public string Id { get; set; }
            public Dictionary<string, string> Environment { get; } = new Dictionary<string, string>();
            public IReadOnlyList<string> Shell { get; set; } = new string[] {};
            
            
        }

        private class RunCommandFailedException : Exception
        {
            public RunCommandFailedException(string message, int exitCode) : base(message) {
                ExitCode = exitCode;
            }

            public int ExitCode { get; }
        }
        
        public async Task<int> RunBuild(Protocol.RunDockerBuild build, IOutputObserver outputObserver, CancellationToken cancellationToken) {
            await CleanupNetworks(cancellationToken);
            
            CreateContainerResponse? proxyContainer = null;

            try {
                proxyContainer = await docker.Containers.CreateContainerAsync(new CreateContainerParameters {
                    Image = build.ProxyImage,
                    Env = build.EnableNetwork
                        ? null
                        : new List<string> {
                            "HELIUM_PROXY_REPLAY=true"
                        },
                    HostConfig = new HostConfig {
                        AutoRemove = true,
                        NetworkMode = build.EnableNetwork ? null : "none",
                        Isolation = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "process" : null,
                    },
                }, cancellationToken);

                NetworksCreateResponse? network = null;
                try {
                    var networkName = networkTag + "-" + proxyContainer.ID;
                    network = await docker.Networks.CreateNetworkAsync(
                        new NetworksCreateParameters {
                            Name = networkName,
                            Internal = true,
                        },
                        cancellationToken
                    );

                    await docker.Networks.ConnectNetworkAsync(
                        network.ID,
                        new NetworkConnectParameters {
                            Container = proxyContainer.ID,
                            EndpointConfig = new EndpointSettings {
                                Aliases = new List<string> {
                                    buildProxyHostname,
                                },
                            },
                        },
                        cancellationToken
                    );
                    
                    
                    var buildMapping = new Dictionary<string, string>();
                    string? imageId = null;

                    foreach(var imageBuild in build.Dockerfile.Builds) {
                        var id = await RunSingleImageBuild(imageBuild, network.ID, buildMapping, outputObserver, cancellationToken);
                        if(imageBuild.FromCommand.AsName != null) {
                            buildMapping.Add(imageBuild.FromCommand.AsName, id);
                        }

                        imageId = id;
                    }

                    if(imageId == null) {
                        throw new Exception("No images were built");
                    }

                    await using(var imageFile = File.Create(build.OutputFile)) {
                        await docker.Images.SaveImageAsync(imageId, cancellationToken);
                    }

                    return 0;
                }
                finally {
                    if(network != null) {
                        await docker.Networks.DeleteNetworkAsync(network.ID, cancellationToken);
                    }
                }
            }
            catch(RunCommandFailedException ex) {
                await outputObserver.StandardOutput(ex.Message);
                return ex.ExitCode;
            }
            catch(Exception ex) {
                await outputObserver.StandardOutput(ex.Message);
                return 1;
            }
            finally {
                if(proxyContainer != null) {
                    await docker.Containers.RemoveContainerAsync(
                        proxyContainer.ID,
                        new ContainerRemoveParameters {Force = true},
                        cancellationToken
                    );
                }
            }
        }

        private async Task<string> RunSingleImageBuild(DockerfileBuild imageBuild, string proxyNetworkId, IReadOnlyDictionary<string, string> buildMapping, IOutputObserver outputObserver, CancellationToken cancellationToken) {
            string id = imageBuild.FromCommand.Image;

            if(id == "scratch") {
                throw new Exception("FROM scratch is not supported");
            }

            var state = await InitialBuildState(id);

            foreach(var command in imageBuild.Commands) {
                var prevId = id;
                id = command switch {
                    
                    RunExecCommand runExec => await HandleRunExecCommand(state, proxyNetworkId, runExec, outputObserver, cancellationToken),
                    RunShellCommand runShell => await HandleRunShellCommand(state, proxyNetworkId, runShell, outputObserver, cancellationToken),
                    _ => throw new Exception("Unexpected command"),
                };

                if(prevId != imageBuild.FromCommand.Image) {
                    await docker.Images.DeleteImageAsync(prevId, new ImageDeleteParameters(), cancellationToken);
                }

                state.Id = id;
            }

            return id;
        }

        private async Task<BuildState> InitialBuildState(string id) {
            var imageInfo = await docker.Images.InspectImageAsync(id);
            if(imageInfo.Config.OnBuild.Count > 0) {
                throw new Exception("ONBUILD is not supported");
            }
            
            var state = new BuildState(id);

            foreach(var envVar in imageInfo.Config.Env) {
                int eqPos = envVar.IndexOf('=');
                if(eqPos < 0) continue;
                state.Environment.Add(envVar.Substring(0, eqPos), envVar.Substring(eqPos + 1));
            }

            state.Shell = imageInfo.Config.Shell.ToList();

            return state;
        }

        private Task<string> HandleRunExecCommand(BuildState state, string proxyNetworkId, RunExecCommand runExec, IOutputObserver outputObserver, CancellationToken cancellationToken) =>
            HandleRunCommandCommon(state, proxyNetworkId, runExec.BuildArgs, runExec.ExecCommand, outputObserver, cancellationToken);

        private Task<string> HandleRunShellCommand(BuildState state, string proxyNetworkId, RunShellCommand runShell, IOutputObserver outputObserver, CancellationToken cancellationToken) {
            var cmd = state.Shell.ToList();
            cmd.Add(runShell.ShellCommand);
            return HandleRunCommandCommon(state, proxyNetworkId, runShell.BuildArgs, cmd, outputObserver, cancellationToken);
        }
            

        private async Task<string> HandleRunCommandCommon(BuildState state, string proxyNetworkId, ReadOnlyDictionary<string, string> buildArgs, IReadOnlyList<string> command, IOutputObserver outputObserver, CancellationToken cancellationToken) {

            var nonEnvArgs = buildArgs.Where(arg => !state.Environment.ContainsKey(arg.Key)).ToList();

            var response = await docker.Containers.CreateContainerAsync(
                new Docker.DotNet.Models.CreateContainerParameters {
                    Image = state.Id,
                    Env = nonEnvArgs.Select(env => $"{env.Key}={env.Value}").Concat(proxyEnv).ToList(),
                    AttachStderr = true,
                    AttachStdout = true,
                    Cmd = command.ToList(),

                    HostConfig = new Docker.DotNet.Models.HostConfig {
                        NetworkMode = proxyNetworkId,
                        Isolation = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "process" : null,
                    },
                },
                cancellationToken
            );

            try {
                using var stream = await docker.Containers.AttachContainerAsync(
                    response.ID,
                    tty: false,
                    new Docker.DotNet.Models.ContainerAttachParameters {
                        Stream = true,
                        Stderr = true,
                        Stdout = true,
                    },
                    cancellationToken
                );

                await docker.Containers.StartContainerAsync(
                    response.ID,
                    new Docker.DotNet.Models.ContainerStartParameters { },
                    cancellationToken
                );

                await DockerHelpers.PipeContainerOutput(outputObserver, stream, cancellationToken);

                var waitResponse = await docker.Containers.WaitContainerAsync(response.ID, cancellationToken);
                int exitCode = (int)waitResponse.StatusCode;
                if(exitCode != 0) {
                    throw new RunCommandFailedException($"Command failed with exit code {exitCode}.", exitCode);
                }

                var image = await docker.Images.CommitContainerChangesAsync(new CommitContainerChangesParameters {
                    ContainerID = response.ID,
                    Config = new Config {
                        Env = nonEnvArgs.Select(env => env.Key).ToList(),
                    },
                }, cancellationToken);

                return image.ID;
            }
            finally {
                await docker.Containers.RemoveContainerAsync(response.ID, new Docker.DotNet.Models.ContainerRemoveParameters(), cancellationToken);
            }
           
            
        }
        
        private static string OutputImageTag(string proxyContainerId) => "helium-build/output-image-for-" + proxyContainerId;
        
        private async Task CleanupNetworks(CancellationToken cancellationToken) {
            try {
                var networks = await docker.Networks.ListNetworksAsync(new NetworksListParameters {
                    Filters = new Dictionary<string, IDictionary<string, bool>> {
                        {
                            "type",
                            new Dictionary<string, bool> {
                                { "custom", true }
                            }
                        },
                        {
                            "label",
                            new Dictionary<string, bool> {
                                { networkTag, true }
                            }
                        },
                    },
                }, CancellationToken.None);

                foreach(var network in networks) {
                    if(!network.Labels.TryGetValue(networkTag, out var proxyContainerId)) {
                        continue;
                    }
                    
                    try {
                        await docker.Containers.InspectContainerAsync(proxyContainerId, cancellationToken);
                        continue;
                    }
                    catch(DockerContainerNotFoundException) {}

                    try {
                        await docker.Images.DeleteImageAsync(OutputImageTag(proxyContainerId), new ImageDeleteParameters(), cancellationToken);
                    }
                    catch(DockerContainerNotFoundException) {}

                    var networkInspect = await docker.Networks.InspectNetworkAsync(network.ID, cancellationToken);
                    foreach(var containerId in networkInspect.Containers.Keys) {
                        await docker.Containers.StopContainerAsync(containerId, new ContainerStopParameters(), cancellationToken);
                        await docker.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters(), cancellationToken);
                    }
                    
                    await docker.Networks.DeleteNetworkAsync(network.ID, cancellationToken);
                }
            }
            catch(Exception ex) {
                Console.WriteLine(ex);
            }
        }


    }
}