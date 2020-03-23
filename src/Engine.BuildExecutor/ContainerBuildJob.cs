using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using Helium.Engine.BuildExecutor.Protocol;
using Helium.Util;

namespace Helium.Engine.BuildExecutor
{
    internal static class ContainerBuildJob
    {

        private const string networkTag = "helium-container-build-proxy";
        private const string buildProxyHostname = "helium-container-build-proxy";
        private const string proxySpec = "http://" + buildProxyHostname + ":8000";

        public static async Task<int> RunBuild(DockerClient client, Protocol.RunDockerBuild build, IOutputObserver outputObserver, CancellationToken cancellationToken) {
            await CleanupNetworks(client, cancellationToken);

            CreateContainerResponse? proxyContainer = null;
            try {
                proxyContainer = await client.Containers.CreateContainerAsync(new CreateContainerParameters {
                    Image = build.ProxyImage,
                    HostConfig = new HostConfig {
                        AutoRemove = true,
                        Isolation = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "process" : null,
                    },
                }, cancellationToken);

                NetworksCreateResponse? network = null;
                try {
                    var networkName = networkTag + "-" + proxyContainer.ID;
                    network = await client.Networks.CreateNetworkAsync(
                        new NetworksCreateParameters {
                            Name = networkName,
                            Internal = true,
                        },
                        cancellationToken
                    );
                    
                    await client.Networks.ConnectNetworkAsync(
                        network.ID,
                        new NetworkConnectParameters {
                            Container = proxyContainer.ID,
                            EndpointConfig = new EndpointSettings {
                                Aliases = {
                                    buildProxyHostname,
                                },
                            }
                        },
                        cancellationToken
                    );

                    var imageTag = OutputImageTag(proxyContainer.ID);

                    var buildParams = new ImageBuildParameters {
                        NoCache = true,
                        ForceRemove = true,
                        NetworkMode = network.ID,
                        Tags = {
                            imageTag,
                        },
                    };

                    foreach(var (arg, value) in build.BuildArgs) {
                        buildParams.BuildArgs[arg] = value;
                    }

                    buildParams.BuildArgs["HTTP_PROXY"] = proxySpec;
                    buildParams.BuildArgs["http_proxy"] = proxySpec;
                    buildParams.BuildArgs["HTTPS_PROXY"] = proxySpec;
                    buildParams.BuildArgs["https_proxy"] = proxySpec;


                    try {
                        await using(var workspaceStream = File.OpenRead(build.WorkspaceTar)) {
                            await using var buildStream = await client.Images.BuildImageFromDockerfileAsync(
                                workspaceStream,
                                buildParams,
                                cancellationToken
                            );

                            var buffer = new byte[4096];
                            int bytesRead;
                            while((bytesRead = await buildStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0) {
                                await outputObserver.StandardOutput(buffer, bytesRead);
                            }
                        }

                        await using(var image = await client.Images.SaveImageAsync(imageTag, cancellationToken)) {
                            await using var savedImageFile = File.Create(build.OutputFile);
                            await image.CopyToAsync(savedImageFile, cancellationToken);
                        }

                        FileUtil.SetUnixMode(build.OutputFile, (6 << 6) | (4 << 3) | 4);
                    }
                    finally {
                        await client.Images.DeleteImageAsync(imageTag, new ImageDeleteParameters(), cancellationToken);
                    }
                }
                finally {
                    if(network != null) {
                        await client.Networks.DeleteNetworkAsync(network.ID, cancellationToken);
                    }
                }
            }
            finally {
                if(proxyContainer != null) {
                    await client.Containers.RemoveContainerAsync(
                        proxyContainer.ID,
                        new ContainerRemoveParameters {
                            Force = true,
                        },
                        cancellationToken);
                }
            }
            
            

            return 0;
        }

        private static string OutputImageTag(string proxyContainerId) => "helium-build/output-image-for-" + proxyContainerId;


        private static async Task CleanupNetworks(IDockerClient client, CancellationToken cancellationToken) {
            try {
                var networks = await client.Networks.ListNetworksAsync(new NetworksListParameters {
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
                        await client.Containers.InspectContainerAsync(proxyContainerId, cancellationToken);
                        continue;
                    }
                    catch(DockerContainerNotFoundException) {}

                    try {
                        await client.Images.DeleteImageAsync(OutputImageTag(proxyContainerId), new ImageDeleteParameters(), cancellationToken);
                    }
                    catch(DockerContainerNotFoundException) {}

                    var networkInspect = await client.Networks.InspectNetworkAsync(network.ID, cancellationToken);
                    foreach(var containerId in networkInspect.Containers.Keys) {
                        await client.Containers.StopContainerAsync(containerId, new ContainerStopParameters(), cancellationToken);
                        await client.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters(), cancellationToken);
                    }
                    
                    await client.Networks.DeleteNetworkAsync(network.ID, cancellationToken);
                }
            }
            catch(Exception ex) {
                Console.WriteLine(ex);
            }
        }
        
    }
}