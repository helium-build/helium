using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Helium.Util;

namespace Helium.Engine.Proxy
{
    public static class Cache
    {
        public static Task CacheDownload(string cacheDir, string outFile, Func<string, Task> download) {
            MutexHelper.Lock(Path.Combine(cacheDir, "cache.lock"), () => {
                if(!File.Exists(outFile)) {
                    string? tempFile = null;
                    try {
                        using(FileUtil.CreateTempFile(cacheDir, out tempFile)) {}
                        
                        try {
                            download(tempFile).Wait();
                        }
                        catch (AggregateException ex) when (ex.InnerException != null && ex.InnerExceptions.Count == 1)
                        {
                            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                            throw;
                        }
                        Directory.CreateDirectory(Path.GetDirectoryName(outFile));
                        File.Move(tempFile, outFile);
                    }
                    finally {
                        try { if(tempFile != null) File.Delete(tempFile); }
                        catch {}
                    }
                }
            }, CancellationToken.None);
            
            return Task.FromResult<object?>(null);
        }
        
    }
}