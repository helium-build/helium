using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
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
    public class SdkInstallManager
    {
        private const string mutexId = "Global\\{f6157371-da3d-47e5-b825-83bc6a6fd60e}";
        
        public SdkInstallManager(string cacheDir) {
            this.cacheDir = cacheDir;
            baseDir = Path.Combine(cacheDir, "sdk");
        }

        private readonly string cacheDir;
        private readonly string baseDir;
        private readonly AsyncLock localLock = new AsyncLock();
        
        private readonly ConcurrentDictionary<string, Task<(string hash, string installDir)>> sdkStore = new ConcurrentDictionary<string, Task<(string hash, string installDir)>>();
        

        public Task<(string hash, string installDir)> GetInstalledSdkDir(SdkInfo sdk) {
            var sdkHash = SdkLoader.sdkSha256(sdk);
            return sdkStore.GetOrAdd(sdkHash, _ => InstalledSdkUncached(sdk, sdkHash));
        }

        private async Task<(string hash, string installDir)> InstalledSdkUncached(SdkInfo sdk, string sdkHash) {
            using var _ = await localLock.LockAsync();
            using var semaphore = new Semaphore(1, 1, mutexId);
            semaphore.WaitOne();
            try {
                Directory.CreateDirectory(baseDir);
                
                var sdkDir = Path.Combine(baseDir, sdkHash);
                var installDir = Path.Combine(sdkDir, "install");
                if(Directory.Exists(sdkDir)) {
                    return (sdkHash, installDir);
                }

                var tempDir = Path.Combine(baseDir, Path.GetRandomFileName());
                var tempInstallDir = Path.Combine(tempDir, "install");
                Directory.CreateDirectory(tempInstallDir);

                await InstallSdk(sdk, sdkHash, tempInstallDir);
                
                Directory.Move(tempDir, sdkDir);

                return (sdkHash, installDir);
            }
            finally {
                semaphore.Release();
            }
        }

        private async Task InstallSdk(SdkInfo sdk, string sdkHash, string installDir) {
            foreach(var step in sdk.setupSteps) {
                switch(step) {
                    case SdkSetupStep.Download download:
                    {
                        if(!PathUtil.IsValidSubPath(download.fileName)) {
                            throw new Exception("Download filenames may not contain . or .. directories");
                        }

                        var fileName = Path.Combine(installDir, download.fileName);
                        await HttpUtil.FetchFile(download.url, fileName);
                        await ValidateHash(download.Item3, fileName);
                    }
                        break;

                    case SdkSetupStep.Extract extract:
                    {
                        if(!PathUtil.IsValidSubPath(extract.fileName) || !PathUtil.IsValidSubPath(extract.directory)) {
                            throw new Exception("Extract filenames may not contain . or .. directories");
                        }

                        var fileName = Path.Combine(installDir, extract.fileName);
                        var directory = Path.Combine(installDir, extract.directory);

                        if(fileName.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase)) {
                            ZipFile.ExtractToDirectory(fileName, directory);
                        }
                        else if(fileName.EndsWith(".tar.gz", StringComparison.InvariantCultureIgnoreCase)) {
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
                            throw new Exception("Delete filenames may not contain . or .. directories");
                        }
                        
                        var fileName = Path.Combine(installDir, delete.fileName);
                        File.Delete(fileName);
                    }
                        break;

                    case SdkSetupStep.CreateDirectory createDirectory:
                    {
                        if(!PathUtil.IsValidSubPath(createDirectory.fileName)) {
                            throw new Exception("CreateDirectory filenames may not contain . or .. directories");
                        }
                        
                        var fileName = Path.Combine(installDir, createDirectory.fileName);
                        Directory.CreateDirectory(fileName);
                    }
                        break;

                    case SdkSetupStep.CreateFile createFile:
                    {
                        if(!PathUtil.IsValidSubPath(createFile.fileName)) {
                            throw new Exception("CreateFile filenames may not contain . or .. directories");
                        }
                        
                        var fileName = Path.Combine(installDir, createFile.fileName);
                        await File.WriteAllTextAsync(fileName, createFile.content, Encoding.UTF8);
                        ArchiveUtil.MakeExecutable(fileName);
                    }
                        break;
                    
                    
                    default:
                        throw new Exception("Unexpected setup step.");

                }
            }
        }

        private async Task ValidateHash(SdkHash hash, string fileName) {
            await using var stream = File.OpenRead(fileName);
            bool isValid = hash switch {
                SdkHash.Sha256 sha256 => await HashUtil.ValidateSha256(stream, sha256.Item),
                SdkHash.Sha512 sha512 => await HashUtil.ValidateSha512(stream, sha512.Item),
                _ => throw new Exception("Unexpected hash type."),
            };
            
            if(!isValid) {
                throw new Exception($"Unexpected hash for downloaded file {fileName}");
            }
        }
    }
}