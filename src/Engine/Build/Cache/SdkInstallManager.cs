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

namespace Helium.Engine.Build.Cache
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
            var sdkHash = SdkLoader.SdkSha256(sdk);
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

                CleanupTempSdkDirs(baseDir);

                Console.Error.WriteLine($"Installing SDK for {sdk.Implements.First()} version {sdk.Version}");

                using var tempDirCleanup = DirectoryCleanup.CreateTempDir(baseDir, prefix: "temp-");

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

        private void CleanupTempSdkDirs(string baseDir) {
            foreach(var tempDir in Directory.EnumerateDirectories(baseDir, "temp-*")) {
                try {
                    Directory.Delete(tempDir, recursive: true);
                }
                catch {}
            }
        }

        private async Task InstallSdk(SdkInfo sdk, string sdkHash, string installDir) {
            await SdkLoader.SaveSdk(Path.Combine(installDir, "../sdk.json"), sdk);
            
            foreach(var step in sdk.SetupSteps) {
                switch(step) {
                    case SdkSetupStep.Download download:
                    {
                        if(!PathUtil.IsValidSubPath(download.FileName)) {
                            throw new Exception($"Download filenames may not contain .. directories: {download.FileName}");
                        }

                        var fileName = Path.Combine(installDir, download.FileName);
                        await HttpUtil.FetchFileValidate(download.Url, fileName, download.Hash.Validate);
                    }
                        break;

                    case SdkSetupStep.Extract extract:
                    {
                        if(!PathUtil.IsValidSubPath(extract.FileName)) {
                            throw new Exception($"Extract filenames may not contain .. directories: {extract.FileName}");
                        }
                        
                        if(!PathUtil.IsValidSubPath(extract.Directory)) {
                            throw new Exception($"Extract filenames may not contain .. directories: {extract.Directory}");
                        }

                        var fileName = Path.Combine(installDir, extract.FileName);
                        var directory = Path.Combine(installDir, extract.Directory);

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
                        if(!PathUtil.IsValidSubPath(delete.FileName)) {
                            throw new Exception("Delete filenames may not contain .. directories");
                        }
                        
                        var fileName = Path.Combine(installDir, delete.FileName);
                        File.Delete(fileName);
                    }
                        break;

                    case SdkSetupStep.CreateDirectory createDirectory:
                    {
                        if(!PathUtil.IsValidSubPath(createDirectory.FileName)) {
                            throw new Exception("CreateDirectory filenames may not contain .. directories");
                        }
                        
                        var fileName = Path.Combine(installDir, createDirectory.FileName);
                        Directory.CreateDirectory(fileName);
                    }
                        break;

                    case SdkSetupStep.CreateFile createFile:
                    {
                        if(!PathUtil.IsValidSubPath(createFile.FileName)) {
                            throw new Exception("CreateFile filenames may not contain .. directories");
                        }
                        
                        var fileName = Path.Combine(installDir, createFile.FileName);
                        await File.WriteAllTextAsync(fileName, createFile.Content, Globals.HeliumEncoding);
                        FileUtil.MakeExecutable(fileName);
                    }
                        break;
                    
                    
                    default:
                        throw new Exception("Unexpected setup step.");

                }
            }
        }
    }
}