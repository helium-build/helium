using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Helium.Engine.Build.Record;
using Helium.Util;
using Microsoft.AspNetCore.Routing;

namespace Helium.Engine.Build.Proxy
{
    internal sealed class MavenProxy
    {
        public MavenProxy(IRecorder recorder, string name, string serverBaseUrl) {
            this.recorder = recorder;
            this.name = name;
            this.serverBaseUrl = serverBaseUrl;
        }

        private const string mode = "maven";
        
        private readonly IRecorder recorder;
        private readonly string name;
        private readonly string serverBaseUrl;

        public Task<string> GetArtifact(string path) =>
            recorder.RecordArtifact("maven/" + name + "/" + path, async cacheDir => {
                var finalFileName = Path.Combine(cacheDir, "dependencies", mode, path);

                await Cache.CacheDownload(cacheDir, finalFileName, async tempFile => {
                    var url = serverBaseUrl;
                    if(!url.EndsWith("/")) url += "/";
                    url += path;
                    
                    try {
                        await HttpUtil.FetchFile(url, tempFile);
                    }
                    catch(WebException ex) when ((ex.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound) {
                        throw new HttpErrorCodeException(HttpStatusCode.NotFound);
                    }
                });

                return finalFileName;
            });
    }
}