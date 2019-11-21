using System;
using System.IO;
using System.Threading.Tasks;
using Helium.Engine.Record;
using Helium.Util;
using Microsoft.AspNetCore.Routing;

namespace Helium.Engine.Proxy
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
                    
                    await HttpUtil.FetchFile(url, tempFile);
                });

                return finalFileName;
            });
    }
}