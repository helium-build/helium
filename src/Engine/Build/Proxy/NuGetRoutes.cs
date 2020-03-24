using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using DotLiquid.Util;
using Helium.Engine.Conf;
using Helium.Engine.Build.Record;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Helium.Engine.Build.Proxy
{
    internal class NuGetRoutes
    {
        private NuGetRoutes(Dictionary<string, NuGetProxy> proxies, IArtifactSaver artifact) {
            this.proxies = proxies;
            this.artifact = artifact;
        }

        private const string packageIdFormat = @"^[a-z0-9_\-][a-z0-9_\-\.]*$";
        private const string packageVersionFormat = @"^[a-zA-Z0-9][a-zA-Z0-9\-+\.]*$";
        
        private readonly Dictionary<string, NuGetProxy> proxies;
        private readonly IArtifactSaver artifact;

        public void Register(IEndpointRouteBuilder endpoint) {
            endpoint.MapGetHead("nuget/v3/{proxyName}/index.json", async (context, isGet) => {
                var proxyName = (string)context.GetRouteValue("proxyName");
                if(!proxies.ContainsKey(proxyName)) {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                context.Response.PrepareJson();
                if(isGet) {
                    await context.Response.SendJson(new NuGetServiceIndex {
                        version = "3.0.0",
                        resources = {
                            new NuGetServiceResource {
                                Id = $"http://localhost:9000/nuget/v3/{proxyName}/package",
                                Type = "PackageBaseAddress/3.0.0",
                            },
                            new NuGetServiceResource {
                                Id = $"http://localhost:9000/nuget/publish",
                                Type = "PackagePublish/2.0.0",
                            },
                            new NuGetServiceResource {
                                Id = $"http://localhost:9000/nuget/v3/{proxyName}/registration",
                                Type = "RegistrationsBaseUrl/3.0.0-rc",
                            },
                            new NuGetServiceResource {
                                Id = $"http://localhost:9000/nuget/v3/{proxyName}/query",
                                Type = "SearchQueryService/3.0.0-rc",
                            },
                        },
                    });
                }
            });
            
            
            endpoint.MapGetHead("nuget/v3/{proxyName}/package/{packageId:regex(" + packageIdFormat + ")}/index.json", async (context, isGet) => {
                var proxyName = (string)context.GetRouteValue("proxyName");
                var packageId = (string) context.GetRouteValue("packageId");
                if(!proxies.TryGetValue(proxyName, out var proxy)) {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                var json = await proxy.GetPackageIndex(packageId);

                context.Response.PrepareJson();
                if(isGet) {
                    await context.Response.SendJson(json);
                }
            });
            
            
            endpoint.MapGetHead("nuget/v3/{proxyName}/package/{packageId:regex(" + packageIdFormat + ")}/{packageVersion:regex(" + packageVersionFormat + ")}/{fileName}.nupkg", async (context, isGet) => {
                var proxyName = (string)context.GetRouteValue("proxyName");
                var packageId = (string) context.GetRouteValue("packageId");
                var packageVersion = (string) context.GetRouteValue("packageVersion");
                var fileName = (string) context.GetRouteValue("fileName");
                
                if(fileName != packageId + "." + packageVersion || !proxies.TryGetValue(proxyName, out var proxy)) {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                var file = await proxy.GetPackage(packageId, packageVersion);
                if(isGet) {
                    await context.Response.SendFileAsync(file);
                }
            });
            
            endpoint.MapGetHead("nuget/v3/{proxyName}/package/{packageId:regex(" + packageIdFormat + ")}/{packageVersion:regex(" + packageVersionFormat + ")}/{fileName}.nuspec", async (context, isGet) => {
                var proxyName = (string)context.GetRouteValue("proxyName");
                var packageId = (string) context.GetRouteValue("packageId");
                var packageVersion = (string) context.GetRouteValue("packageVersion");
                var fileName = (string) context.GetRouteValue("fileName");
                
                if(fileName != packageId + "." + packageVersion || !proxies.TryGetValue(proxyName, out var proxy)) {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                var file = await proxy.GetPackage(packageId, packageVersion);

                var nuspecData = await ReadNuSpec(file);
                if(nuspecData == null) {
                    context.Response.StatusCode = (int) HttpStatusCode.NotFound;
                    return;
                }

                if(isGet) {
                    await context.Response.WriteAsync(nuspecData, Encoding.UTF8);
                }
            });

            endpoint.MapMethods("nuget/v3/{proxyName}/publish/{packageId}/{packageVersion}", new[] { "DELETE", "POST" }, async context => {
                var proxyName = (string)context.GetRouteValue("proxyName");
                if(!proxies.ContainsKey(proxyName)) {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            });

            endpoint.MapGetHead("nuget/v3/{proxyName}/registration/{packageId}/index.json", async (context, _) => {
                var proxyName = (string)context.GetRouteValue("proxyName");
                if(!proxies.ContainsKey(proxyName)) {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            });
            
            endpoint.MapGetHead("nuget/v3/{proxyName}/query", async (context, isGet) => {
                var proxyName = (string)context.GetRouteValue("proxyName");
                if(!proxies.ContainsKey(proxyName)) {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                await context.Response.SendJson(new JObject {
                    ["totalHits"] = 0,
                    ["data"] = new JArray(),
                });
            });

            endpoint.MapPut("nuget/publish", async context => {
                try {
                    var uploadFile = context.Request.Form.Files.First();
                    await using var uploadStream = uploadFile.OpenReadStream();
                    
                    await artifact.SaveArtifact(uploadStream, async file => {
                        try {
                            var nuspecData = await ReadNuSpec(file);

                            var xmlFile = XDocument.Parse(nuspecData);

                            XNamespace xmlns = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"; 
                            
                            var metadata = xmlFile.Root?.Element(xmlns + "metadata");
                            var id = metadata?.Element(xmlns + "id")?.Value?.ToLowerInvariant();
                            var version = metadata?.Element(xmlns + "version")?.Value;
                            
                            if(id == null || version == null || !Regex.IsMatch(id, packageIdFormat) || !Regex.IsMatch(version, packageVersionFormat)) {
                                throw new Exception("Invalid nuspec.");
                            }

                            return id + "." + version + ".nupkg";
                        }
                        catch(Exception) {
                            throw new HttpErrorCodeException(HttpStatusCode.BadRequest);
                        }
                    });
                }
                catch(HttpErrorCodeException ex) {
                    context.Response.StatusCode = (int)ex.ErrorCode;
                }
            });
        }

        private static async Task<string?> ReadNuSpec(string file) {
            using var zip = ZipFile.OpenRead(file);
            var nuspec = zip.Entries.FirstOrDefault(entry => entry.Name.EndsWith(".nuspec"));
            if(nuspec == null) {
                return null;
            }

            await using var stream = nuspec.Open();
            var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        public static NuGetRoutes Build(IRecorder recorder, IArtifactSaver artifact, Config config) {
            var proxies = new Dictionary<string, NuGetProxy>();
            foreach(var repo in config.repos.nuget) {
                var name = repo.name ?? throw new Exception("NuGet repo name is missing.");
                var url = repo.url ?? throw new Exception("NuGet repo url is missing.");
                
                proxies.Add(name, new NuGetProxy(recorder, name, url));
            }
            
            return new NuGetRoutes(proxies, artifact);
        }


        internal class NuGetServiceIndex
        {
            public string? version { get; set; }
            public List<NuGetServiceResource> resources { get; set; } = new List<NuGetServiceResource>();
        }
        
        internal class NuGetServiceResource
        {
            [JsonProperty("@id")]
            public string? Id { get; set; }
            
            [JsonProperty("@type")]
            public string? Type { get; set; }
        }
    }
}