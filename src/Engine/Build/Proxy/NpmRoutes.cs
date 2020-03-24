using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DotLiquid.Util;
using Helium.Engine.Conf;
using Helium.Engine.Build.Record;
using Helium.Util;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Helium.Engine.Build.Proxy
{
    internal class NpmRoutes
    {
        private NpmRoutes(IRecorder recorder, IArtifactSaver artifact, string registryUrl) {
            this.recorder = recorder;
            this.artifact = artifact;
            this.registryUrl = registryUrl;
        }

        private const string namePartFormat = @"[^\.\\/][^\\/]*";
        private const string nameFormat = @"^[^\.\\/][^\\/]*$";
        private const string scopeFormat = @"^@[^\.\\/][^\\/]*$";

        private readonly IRecorder recorder;
        private readonly IArtifactSaver artifact;
        private readonly string registryUrl;

        private readonly ConcurrentDictionary<(string? scope, string packageName, string packageVersion), string> packageUrlMapping =
            new ConcurrentDictionary<(string? scope, string packageName, string packageVersion), string>();
        
        public void Register(IEndpointRouteBuilder endpoint) {
            endpoint.MapGetHead("npm/{packageName:regex(" + nameFormat + ")}", HandleRequestMetadataAllVersions);
            endpoint.MapGetHead("npm/{scope:regex(" + scopeFormat + ")}/{packageName:regex(" + nameFormat + ")}", HandleRequestMetadataAllVersions);
            
            endpoint.MapGetHead("npm/{packageName:regex(" + nameFormat + ")}/{packageVersion}", HandleRequestMetadataVersion);
            endpoint.MapGetHead("npm/{scope:regex(" + scopeFormat + ")}/{packageName:regex(" + nameFormat + ")}/{packageVersion}", HandleRequestMetadataVersion);
            
            endpoint.MapGetHead("npm/{packageName:regex(" + nameFormat + ")}/-/{packageVersion}.tgz", HandleRequestArtifact);
            endpoint.MapGetHead("npm/{scope:regex(" + scopeFormat + ")}/{packageName:regex(" + nameFormat + ")}/-/{packageVersion}.tgz", HandleRequestArtifact);
            
            endpoint.MapPut("npm/{packageName:regex(" + nameFormat + ")}", HandlePublishPackage);
            endpoint.MapPut("npm/{scope:regex(" + scopeFormat + ")}/{packageName:regex(" + nameFormat + ")}", HandlePublishPackage);
            
            endpoint.MapPost("npm/-/npm/v1/security/audits/quick", async context => {
                await context.Response.SendJson(new JObject {
                    ["actions"] = new JArray(),
                    ["advisories"] = new JObject(),
                    ["muted"] = new JArray(),
                    ["metadata"] = new JObject {
                        ["vulnerabilities"] = new JObject {
                            ["info"] = 0,
                            ["low"] = 0,
                            ["moderate"] = 0,
                            ["high"] = 0,
                            ["critical"] = 0,
                        },
                        
                        ["dependencies"] = 3,
                        ["devDependencies"] = 196,
                        ["optionalDependencies"] = 0,
                        ["totalDependencies"] = 199,
                    },
                });
            });
        }

        private async Task HandleRequestMetadataAllVersions(HttpContext context, bool isGet) {
            var scope = (string?) context.GetRouteValue("scope");
            var packageName = (string) context.GetRouteValue("packageName");

            var path = "npm/";
            if(scope != null) path += scope + "/";
            path += packageName;

            var json = await recorder.RecordTransientMetadata(path, async () => {
                var url = registryUrl;
                if(!url.EndsWith("/")) url += "/";
                url += packageName;

                var npmJson = await HttpUtil.FetchJson<JObject>(url);
                
                try {
                    if(npmJson == null) {
                        throw new Exception("Metadata response was null.");
                    }
                    
                    var versions = (JObject?)npmJson["versions"];
                    if(versions == null) {
                        throw new Exception("Dist object is null");
                    }

                    foreach(var version in versions.Properties()) {
                        var packageVersion = version.Name;

                        var dist = ((JObject?)version.Value)?["dist"];
                        if(dist == null) {
                            throw new Exception("Dist object is null");
                        }

                        var tarball = "http://localhost:9000/npm/";
                        if(scope != null) tarball += scope + "/";
                        tarball += packageName + "/-/" + packageVersion + ".tgz";

                        var npmUrl = (string?)dist["tarball"] ?? throw new Exception("Old tarball not found.");
                        dist["tarball"] = tarball;

                        packageUrlMapping[(scope, packageName, packageVersion)] = npmUrl;
                    }
                }
                catch {
                    throw new HttpErrorCodeException(HttpStatusCode.NotFound);
                }
                
                return npmJson;
            });

            context.Response.PrepareJson();
            if(isGet) {
                await context.Response.SendJson(json);
            }
        }

        private async Task HandleRequestMetadataVersion(HttpContext context, bool isGet) {
            var scope = (string?) context.GetRouteValue("scope");
            var packageName = (string) context.GetRouteValue("packageName");
            var packageVersion = (string) context.GetRouteValue("packageVersion");

            try {
                var _ = new SemVer.Version(packageVersion);
            }
            catch {
                context.Response.StatusCode = (int) HttpStatusCode.NotFound;
                return;
            }

            var json = await FetchMetadataVersion(scope, packageName, packageVersion);

            context.Response.PrepareJson();
            if(isGet) {
                await context.Response.SendJson(json);
            }
        }

        private async Task HandleRequestArtifact(HttpContext context, bool isGet) {
            var scope = (string?) context.GetRouteValue("scope");
            var packageName = (string) context.GetRouteValue("packageName");
            var packageVersion = (string) context.GetRouteValue("packageVersion");

            try {
                var _ = new SemVer.Version(packageVersion);
            }
            catch {
                context.Response.StatusCode = (int) HttpStatusCode.NotFound;
                return;
            }

            try {
                var _ = new SemVer.Version(packageVersion);
            }
            catch {
                context.Response.StatusCode = (int) HttpStatusCode.NotFound;
                return;
            }

            var path = "npm/";
            if(scope != null) path += scope + "/";
            path += packageName + "/" + packageVersion + ".tgz";

            var file = await recorder.RecordArtifact(path, async cacheDir => {
                var finalFileName = Path.Combine(cacheDir, "dependencies", "npm");
                if(scope != null) finalFileName = Path.Combine(finalFileName, scope);
                finalFileName = Path.Combine(finalFileName, packageName, packageVersion + ".tgz");

                await Cache.CacheDownload(cacheDir, finalFileName, async tempFile => {
                    await FetchMetadataVersion(scope, packageName, packageVersion);
                    var url = packageUrlMapping[(scope, packageName, packageVersion)];
                    await HttpUtil.FetchFile(url, tempFile);
                });

                return finalFileName;
            });

            if(isGet) {
                await context.Response.SendFileAsync(file);
            }
        }

        private async Task<JObject> FetchMetadataVersion(string? scope, string packageName, string packageVersion) {
            var path = "npm/";
            if(scope != null) path += scope + "/";
            path += packageName;

            var json = await recorder.RecordTransientMetadata(path, async () => {
                var url = registryUrl;
                if(!url.EndsWith("/")) url += "/";
                url += packageName + "/" + packageVersion;

                var npmJson = await HttpUtil.FetchJson<JObject>(url);
                try {
                    if(npmJson == null) {
                        throw new Exception("Metadata response was null.");
                    }
                    
                    var tarball = "http://localhost:9000/npm/";
                    if(scope != null) tarball += scope + "/";
                    tarball += packageName + "/-/" + packageVersion + ".tgz";

                    var dist = npmJson["dist"];
                    if(dist == null) {
                        throw new Exception("Dist object is null");
                    }
                    
                    var npmUrl = (string?)dist["tarball"] ?? throw new Exception("Old tarball not found.");
                    dist["tarball"] = tarball;

                    packageUrlMapping[(scope, packageName, packageVersion)] = npmUrl;
                }
                catch {
                    throw new HttpErrorCodeException(HttpStatusCode.NotFound);
                }

                return npmJson;
            });
            return json;
        }

        private async Task HandlePublishPackage(HttpContext context) {
            var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();

            var artifacts = new List<(string fileName, byte[] data)>();

            try {
                var artifactJson = JObject.Parse(requestBody);
                var attachments = (JObject?) artifactJson["_attachments"];
                if(attachments == null) {
                    throw new Exception("No attachments");
                }

                foreach(var prop in attachments.Properties()) {
                    var b64Value = (string?) prop.Value?["data"];
                    if(b64Value == null) {
                        throw new Exception("Attachment data is null.");
                    }

                    var data = Convert.FromBase64String(b64Value);
                    artifacts.Add((prop.Name, data));
                }
            }
            catch {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            foreach(var (fileName, data) in artifacts) {
                await artifact.SaveArtifact(fileName, new MemoryStream(data));
            }
        }

        public static NpmRoutes? Build(IRecorder recorder, IArtifactSaver artifact, Config config) {
            if(config.repos.npm == null) {
                return null;
            }

            var registryUrl = config.repos.npm.registry ?? throw new Exception("NPM registry url is missing.");
            return new NpmRoutes(recorder, artifact, registryUrl);
        }
    }
}