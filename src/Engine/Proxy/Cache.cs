using System;
using System.IO;
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
                    download(tempFile).Wait();
                    Directory.CreateDirectory(Path.GetDirectoryName(outFile));
                    File.Move(tempFile, outFile);
                }
                finally {
                    File.Delete(tempFile);
                }
            }
            
            return Task.FromResult<object?>(null);
        }
        
    }
}