using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Helium.Sdks;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;

namespace SdkServer
{
    public class SdkResolver : ISdkResolver
    {
        private SdkResolver(Dictionary<string, ISdkPackage> packages) {
            this.packages = packages;
        }
        
        private readonly Dictionary<string, ISdkPackage> packages;

        public ISdkPackage? GetSdk(string name) => packages.TryGetValue(name, out var package) ? package : null;

        public static async ValueTask<ISdkResolver> Build(IAsyncEnumerable<SdkInfo> sdks) {
            var packageDict = await (
                from sdk in sdks
                from packageName in sdk.Implements.ToAsyncEnumerable()
                group sdk by packageName into packages
                select packages
            ).ToDictionaryAwaitAsync(
                async grouped => grouped.Key,
                SdkPackage.Build
            );

            return new SdkResolver(packageDict);
        }
    }

    public class SdkPackage : ISdkPackage
    {
        private SdkPackage(Dictionary<string,IVersionedSdk> verDict) {
            this.verDict = verDict;
        }
        
        private readonly Dictionary<string, IVersionedSdk> verDict;

        public IEnumerable<string> Versions => verDict.Keys;
        public IVersionedSdk? GetVersion(string version) => verDict.TryGetValue(version, out var verSdk) ? verSdk : null;
        
        public static async ValueTask<ISdkPackage> Build(IAsyncEnumerable<SdkInfo> sdks) {
            var verDict = await sdks
                .GroupBy(sdk => sdk.Version)
                .ToDictionaryAwaitAsync(
                    async grouped => grouped.Key,
                    VersionedSdk.Build);
            
            return new SdkPackage(verDict);
        }
    }

    public class VersionedSdk : IVersionedSdk
    {
        private VersionedSdk(Dictionary<PlatformInfo,IPlatformSdk> packageDict) {
            this.packageDict = packageDict;
        }
        
        private readonly Dictionary<PlatformInfo, IPlatformSdk> packageDict;

        public IEnumerable<PlatformInfo> Platforms => packageDict.Keys;

        public IPlatformSdk? GetPlatform(PlatformInfo platform) =>
            packageDict.TryGetValue(platform, out var platSdk) ? platSdk : null;

        public static async ValueTask<IVersionedSdk> Build(IAsyncEnumerable<SdkInfo> sdks) {
            var packageDict = await (
                from sdk in sdks
                from platform in sdk.Platforms.ToAsyncEnumerable()
                group sdk by platform into packages
                select packages
            ).ToDictionaryAwaitAsync(
                async grouped => grouped.Key,
                async grouped => PlatformSdk.Build(await grouped.FirstAsync()));

            return new VersionedSdk(packageDict);
        }
    }

    public class PlatformSdk : IPlatformSdk
    {
        private PlatformSdk(List<string> realUrls, SdkInfo sdk) {
            this.realUrls = realUrls;
            this.sdk = sdk;
        }


        private readonly List<string> realUrls;
        private readonly SdkInfo sdk;

        public SdkInfo SdkForBaseUrl(Uri baseUrl) {
            int nextUrlIndex = 0;
            var newSteps = new List<SdkSetupStep>();
            foreach(var step in sdk.SetupSteps) {
                switch(step) {
                    case SdkSetupStep.Download dl:
                    {
                        int urlIndex = nextUrlIndex;
                        ++nextUrlIndex;
                        newSteps.Add(new SdkSetupStep.Download(
                            url: new Uri(baseUrl, "file/" + urlIndex).ToString(),
                            fileName: dl.FileName,
                            hash: dl.Hash
                        ));
                        break;
                    }
                    
                    default:
                        newSteps.Add(step);
                        break;
                }
            }
            
            return new SdkInfo(
                implements: sdk.Implements,
                version: sdk.Version,
                platforms: sdk.Platforms,
                setupSteps: newSteps,
                pathDirs: sdk.PathDirs,
                env: sdk.Env,
                configFileTemplates: sdk.ConfigFileTemplates
            );
        }


        public string? UrlForFile(int fileId) {
            if(fileId >= 0 && fileId < realUrls.Count) {
                return realUrls[fileId];
            }
            else {
                return null;
            }
        }


        public static IPlatformSdk Build(SdkInfo sdk) {

            var realUrls = sdk.SetupSteps
                .OfType<SdkSetupStep.Download>()
                .Select(dl => dl.Url)
                .ToList();
            
            return new PlatformSdk(realUrls, sdk);
        }

        private static string EncodePlatform(PlatformInfo platform) {
            var json = JsonConvert.SerializeObject(platform, typeof(PlatformInfo), new JsonSerializerSettings());
            var bytes = Encoding.UTF8.GetBytes(json);
            return Base64UrlTextEncoder.Encode(bytes);
        }
    }
}