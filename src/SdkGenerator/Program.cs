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

            foreach(var creator in sdkCreators) {
                await foreach(var (path, sdk) in creator.GenerateSdks()) {
                    await SdkLoader.saveSdk(sdk, Path.Combine(sdkDir, path));
                }
            }
        }

        private void ClearDirectory(string dir) {
            foreach(var subdir in Directory.EnumerateDirectories(dir)) {
                Directory.Delete(subdir, recursive: true);
            }
            
            foreach(var file in Directory.EnumerateFiles(dir)) {
                File.Delete(file);
            }
        }

        private static readonly ISdkCreator[] sdkCreators = {
            new OpenJDK(),
        };
    }
}
