using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Helium.Engine.Proxy
{
    public static class Cache
    {
        private const string mutexId = "Global\\{9a6fcaf3-f70f-4c5c-a153-60d489a7ba9b}";

        public static Task CacheDownload(string cacheDir, string outFile, Func<string, Task> download) {

            using var mutex = new Mutex(true, mutexId);

            if(!File.Exists(outFile)) {
                var tempFile = Path.Combine(cacheDir, Path.GetRandomFileName());
                try {
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
                    try { File.Delete(tempFile); }
                    catch {}
                }
            }
            
            return Task.FromResult<object?>(null);
        }
        
    }
}