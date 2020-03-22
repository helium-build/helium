using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Helium.Sdks;
using Helium.Util;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using Microsoft.AspNetCore.Routing;
using Nito.AsyncEx;

namespace Helium.Engine.Cache
{
    public class SdkInstallManager : ISdkInstallManager
    {
        
        public SdkInstallManager(string cacheDir) {
            this.cacheDir = cacheDir;
            baseDir = Path.Combine(cacheDir, "sdk");
        }

        private readonly string cacheDir;
        private readonly string baseDir;
        
        private readonly ConcurrentDictionary<string, Task<(string hash, string installDir)>> sdkStore = new ConcurrentDictionary<string, Task<(string hash, string installDir)>>();
        

        public virtual Task<(string hash, string installDir)> GetInstalledSdkDir(SdkInfo sdk) {
            var sdkHash = SdkLoader.sdkSha256(sdk);
            return sdkStore.GetOrAdd(sdkHash, _ => Task.Run(() => InstalledSdkUncached(sdk, sdkHash)));
        }

        private (string hash, string installDir) InstalledSdkUncached(SdkInfo sdk, string sdkHash) =>
            MutexHelper.Lock(Path.Combine(baseDir, "sdk.lock"), () => {

                Directory.CreateDirectory(baseDir);

                var sdkDir = Path.Combine(baseDir, sdkHash);
                var installDir = Path.Combine(sdkDir, "install");
                if(Directory.Exists(sdkDir)) {
                    return (sdkHash, installDir);
                }

                Console.Error.WriteLine($"Installing SDK for {sdk.implements.First()} version {sdk.version}");

                using var tempDirCleanup = DirectoryCleanup.CreateTempDir(baseDir);

                var tempDir = tempDirCleanup.Value;
                var tempInstallDir = Path.Combine(tempDir, "install");
                Directory.CreateDirectory(tempInstallDir);

                try {
                    InstallSdk(sdk, sdkHash, tempInstallDir).Wait();
                }
                catch(AggregateException ex) when(ex.InnerException != null && ex.InnerExceptions.Count == 1) {
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                    throw;
                }


                Directory.Move(tempDir, sdkDir);

                return (sdkHash, installDir);
            }, CancellationToken.None);

        private async Task InstallSdk(SdkInfo sdk, string sdkHash, string installDir) {
            await SdkLoader.saveSdk(sdk, Path.Combine(installDir, "../sdk.json"));
            
            foreach(var step in sdk.setupSteps) {
                switch(step) {
                    case SdkSetupStep.Download download:
                    {
                        if(!PathUtil.IsValidSubPath(download.fileName)) {
                            throw new Exception($"Download filenames may not contain .. directories: {download.fileName}");
                        }

                        var fileName = Path.Combine(installDir, download.fileName);
                        await HttpUtil.FetchFileValidate(download.url, fileName, download.Item3.Validate);
                    }
                        break;

                    case SdkSetupStep.Extract extract:
                    {
                        if(!PathUtil.IsValidSubPath(extract.fileName)) {
                            throw new Exception($"Extract filenames may not contain .. directories: {extract.fileName}");
                        }
                        
                        if(!PathUtil.IsValidSubPath(extract.directory)) {
                            throw new Exception($"Extract filenames may not contain .. directories: {extract.directory}");
                        }

                        var fileName = Path.Combine(installDir, extract.fileName);
                        var directory = Path.Combine(installDir, extract.directory);

                        if(fileName.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase)) {
                            ZipFile.ExtractToDirectory(fileName, directory);
                        }
                        else if(fileName.EndsWith(".tar.gz", StringComparison.InvariantCultureIgnoreCase) || fileName.EndsWith(".tgz", StringComparison.InvariantCultureIgnoreCase)) {
                            await using var inStream = File.OpenRead(fileName);
                            await using var gzipStream = new GZipInputStream(inStream);

                            await using var tarStream = new TarInputStream(gzipStream);

                            await ArchiveUtil.ExtractTar(tarStream, directory);
                        }
                        else {
                            throw new Exception($"Unsupported archive type.");                            
                        }
                    }
                        break;

                    case SdkSetupStep.Delete delete:
                    {
                        if(!PathUtil.IsValidSubPath(delete.fileName)) {
                            throw new Exception("Delete filenames may not contain .. directories");
                        }
                        
                        var fileName = Path.Combine(installDir, delete.fileName);
                        File.Delete(fileName);
                    }
                        break;

                    case SdkSetupStep.CreateDirectory createDirectory:
                    {
                        if(!PathUtil.IsValidSubPath(createDirectory.fileName)) {
                            throw new Exception("CreateDirectory filenames may not contain .. directories");
                        }
                        
                        var fileName = Path.Combine(installDir, createDirectory.fileName);
                        Directory.CreateDirectory(fileName);
                    }
                        break;

                    case SdkSetupStep.CreateFile createFile:
                    {
                        if(!PathUtil.IsValidSubPath(createFile.fileName)) {
                            throw new Exception("CreateFile filenames may not contain .. directories");
                        }
                        
                        var fileName = Path.Combine(installDir, createFile.fileName);
                        await File.WriteAllTextAsync(fileName, createFile.content, Globals.HeliumEncoding);
                        ArchiveUtil.MakeExecutable(fileName);
                    }
                        break;
                    
                    
                    default:
                        throw new Exception("Unexpected setup step.");

                }
            }
        }
    }
}