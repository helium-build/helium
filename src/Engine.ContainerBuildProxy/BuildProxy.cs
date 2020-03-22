using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Helium.Util;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Http;
using Titanium.Web.Proxy.Models;
using Nito.AsyncEx;

namespace Helium.Engine.ContainerBuildProxy
{
    public class BuildProxy
    {
        public BuildProxy(string filesDir) {
            this.filesDir = filesDir;
            proxy = new ProxyServer();
            proxy.AddEndPoint(new ExplicitProxyEndPoint(IPAddress.Any, 8000, decryptSsl: true));
            
            proxy.BeforeRequest += ProxyOnBeforeRequest;
            proxy.BeforeResponse += ProxyOnBeforeResponse;
        }

        private readonly string filesDir;
        private readonly ProxyServer proxy;
        
        private readonly AsyncLock fileLock = new AsyncLock();
        private readonly Dictionary<string, TaskCompletionSource<object?>> downloadingFiles = new Dictionary<string, TaskCompletionSource<object?>>();

        public void Start() => proxy.Start();

        public void Stop() => proxy.Stop();

        private async Task ProxyOnBeforeRequest(object sender, SessionEventArgs e) {
            var method = e.HttpClient.Request.Method.ToUpperInvariant();
            var uri = e.HttpClient.Request.RequestUri;

            void Forbidden() {
                e.Respond(new Response {
                    StatusCode = 403,
                    StatusDescription = "Forbidden",
                });
            }

            if(e.IsHttps || (method != "HEAD" && method != "GET") || !string.IsNullOrEmpty(uri.Query) || e.HttpClient.Request.HasBody) {
                Forbidden();
                return;
            }

            string relFileName;
            try {
                relFileName = Path.Combine(uri.Host, uri.Port.ToString(), uri.AbsolutePath);
            }
            catch {
                Forbidden();
                return;
            }
            if(!PathUtil.IsValidSubPath(relFileName)) {
                Forbidden();
                return;
            }

            var fileName = Path.Combine(filesDir, relFileName);

            bool useLocalFile = false;
            TaskCompletionSource<object?>? resultTcs;
            
            using(await fileLock.LockAsync()) {
                if(downloadingFiles.TryGetValue(fileName, out resultTcs)) {
                    await resultTcs.Task;
                    useLocalFile = true;
                }
                else if(File.Exists(fileName)) {
                    useLocalFile = true;
                }
                else {
                    resultTcs = new TaskCompletionSource<object?>();
                    downloadingFiles.Add(fileName, resultTcs);
                }
            }

            if(useLocalFile) {
                e.Ok(await File.ReadAllBytesAsync(fileName));
                return;
            }

            var fileInfo = new SavedFileInfo(
                useHeadResponse: (method == "HEAD"),
                outputFile: fileName,
                resultTcs: resultTcs!
            );
            
            e.UserData = fileInfo;
        }

        private async Task ProxyOnBeforeResponse(object sender, SessionEventArgs e) {
            var fileInfo = (SavedFileInfo)e.UserData;

            if(e.HttpClient.Response.StatusCode != 200) {
                e.Respond(new Response {
                    StatusCode = 500,
                    StatusDescription = "Internal Server Error",
                });
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fileInfo.OutputFile));
            await FileUtil.WriteAllBytesToDiskAsync(fileInfo.OutputFile, await e.GetResponseBody(), CancellationToken.None);
            using(await fileLock.LockAsync()) {
                downloadingFiles.Remove(fileInfo.OutputFile);
                fileInfo.ResultTcs.SetResult(null);
            }
            
            if(fileInfo.UseHeadResponse) {
                e.SetResponseBody(new byte[0]);
            }
        }
    }
}