using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace Helium.Engine.JobExecutor
{
    public static class ContainerBuildJob
    {

        private const string networkTag = "helium-container-build-proxy";

        public static async Task<int> RunBuild(DockerClient client, Protocol.RunDockerBuild build, Stream workspaceStream, CancellationToken cancellationToken) {
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

                using var stream = await client.Containers.AttachContainerAsync(
                    proxyContainer.ID,
                    false,
                    new ContainerAttachParameters {
                        Stream = false,
                    },
                    cancellationToken
                );

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
                        },
                        cancellationToken
                    );

                    var inspectResponse = await client.Containers.InspectContainerAsync(proxyContainer.ID, cancellationToken);
                    var ip = inspectResponse.NetworkSettings.Networks[networkName].IPAddress;

                    var buildStream = await client.Images.BuildImageFromDockerfileAsync(
                        workspaceStream,
                        new ImageBuildParameters {
                            NoCache = true,
                            ForceRemove = true,
                        },
                        cancellationToken
                    );
                    
                    
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

                    if(network.Containers.Count > 0) {
                        continue;
                    }
                    
                    try {
                        await client.Containers.InspectContainerAsync(proxyContainerId, cancellationToken);
                        continue;
                    }
                    catch(DockerContainerNotFoundException) {}

                    await client.Networks.DeleteNetworkAsync(network.ID, cancellationToken);
                }
            }
            catch(Exception ex) {
                Console.WriteLine(ex);
            }
        }
        
    }
}