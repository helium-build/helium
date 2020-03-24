using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Helium.Engine.Build.Record;
using Helium.Util;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json.Linq;

namespace Helium.Engine.Build.Proxy
{
    internal sealed class NuGetProxy
    {
        public NuGetProxy(IRecorder recorder, string name, string serverBaseUrl) {
            this.recorder = recorder;
            this.name = name;
            this.serverBaseUrl = serverBaseUrl;
        }

        private readonly IRecorder recorder;
        private readonly string name;
        private readonly string serverBaseUrl;


        public Task<string> GetPackage(string packageId, string packageVersion) =>
            recorder.RecordArtifact(
                "nuget/" + name + "/" + packageId + "/" + packageVersion + "/" + packageId + "." + packageVersion + ".nupkg", 
                async cacheDir => {
                    var finalFileName = Path.Combine(cacheDir, "dependencies", "nuget", name, packageId, packageVersion, packageId + "." + packageVersion + ".nupkg");

                    await Cache.CacheDownload(cacheDir, finalFileName, async tempFile => {
                        var url = await NuGetPackageBaseAddress();
                        if(!url.EndsWith("/")) url += "/";
                        url += packageId + "/" + packageVersion + "/" + packageId + "." + packageVersion + ".nupkg";

                        await HttpUtil.FetchFile(url, tempFile);
                    });

                    return finalFileName;
                }
            );

        public Task<JObject> GetPackageIndex(string packageId) =>
            recorder.RecordTransientMetadata($"nuget/v3/{name}/{packageId}/index.json", async () => {
                var packageBaseAddress = await NuGetPackageBaseAddress();
                
                var url = packageBaseAddress + (packageBaseAddress.EndsWith("/") ? "" : "/") + packageId + "/index.json";
                return await HttpUtil.FetchJson<JObject>(url);
            });

        private string? packageBaseAddress = null;

        private async Task<string> NuGetPackageBaseAddress() {
            if(packageBaseAddress != null) {
                return packageBaseAddress;
            }
            
            var serviceIndex = await HttpUtil.FetchJson<NuGetRoutes.NuGetServiceIndex>(serverBaseUrl);

            var baseAddr = serviceIndex.resources
               .FirstOrDefault(res => res.Type == "PackageBaseAddress/3.0.0")
               ?.Id ?? throw new HttpErrorCodeException(HttpStatusCode.NotFound);

            Interlocked.CompareExchange(ref packageBaseAddress, baseAddr, null);
            return packageBaseAddress;
        }

    }
}