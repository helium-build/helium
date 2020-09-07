using System;
using System.IO;
using System.Threading.Tasks;
using Helium.Sdks;

namespace Helium.SdkGenerator
{
    internal static class Program
    {
        private static async Task Main(string[] args) {
            string sdkDir = Path.Combine(Environment.CurrentDirectory, "sdks");

            ClearDirectory(sdkDir);
            
            foreach(var creator in sdkCreators) {
                await foreach(var (path, sdk) in creator.GenerateSdks()) {
                    string fullPath = Path.Combine(sdkDir, path);
                    EnsureDirectory(Path.GetDirectoryName(fullPath));
                    await SdkLoader.SaveSdk(fullPath, sdk);
                }
            }
        }

        private static void EnsureDirectory(string? dir) {
            if(dir == null) return;
            
            if(!Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }
        }

        private static void ClearDirectory(string dir) {
            foreach(var subdir in Directory.EnumerateDirectories(dir)) {
                Directory.Delete(subdir, recursive: true);
            }
            
            foreach(var file in Directory.EnumerateFiles(dir)) {
                File.Delete(file);
            }
        }

        private static readonly ISdkCreator[] sdkCreators = {
            new AdoptOpenJDKCreator(),
            new SBTCreator(),
            new DotNetCreator(),
            new DotNetMergeCreator(),
            new NodeJSCreator(),
        };
    }
}
