using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Helium.Sdks;
using Helium.Util;
using Microsoft.FSharp.Collections;

namespace Helium.SdkGenerator
{
    public class DotNetCreator : ISdkCreator
    {
        public string Name => "dotnet";

        private readonly string[] channels = {
            "3.0",
            "2.2",
            "2.1",
        };

        private readonly (SdkOperatingSystem os, string osStr, string ext)[] supportedOSList = {
            (SdkOperatingSystem.Windows, "win", "zip"),
            (SdkOperatingSystem.Linux, "linux", "tar.gz"),
        };

        private readonly (SdkArch arch, string archStr)[] supportedArchList = {
            (SdkArch.Amd64, "x64"),
            (SdkArch.X86, "x86"),
            (SdkArch.Arm, "arm"),
            (SdkArch.Aarch64, "arm64"),
        };


        private const string configFile =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <clear/>
        {{ for repo in repos.nuget }}
        <add key=""{{ repo.name | escape }}"" value=""{{ repo.url | escape }}"" />
        {{ endfor }}
    </packageSources>
    
    <config>
        <add key=""defaultPushSource"" value=""{{ repos.nuget_push_url }}"" />
    </config>
</configuration>
";
        
        private async Task<string> LatestVersionSdk(string channel) {
            var data = await HttpUtil.FetchString($"https://dotnetcli.blob.core.windows.net/dotnet/Sdk/{channel}/latest.version");
            return data.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).First();
        }

        private async Task<string> LatestVersionRuntime(string channel) {
            var data = await HttpUtil.FetchString($"https://dotnetcli.blob.core.windows.net/dotnet/Runtime/{channel}/latest.version");
            return data.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).First();
        }
        
        
        
        public async IAsyncEnumerable<(string path, SdkInfo)> GenerateSdks() {
            foreach(var channel in channels) {

                var verSdk = await LatestVersionSdk(channel);
                var verRuntime = await LatestVersionRuntime(channel);
                var shaMap = HashUtil.ParseSha512File(
                    await HttpUtil.FetchString($"https://dotnetcli.blob.core.windows.net/dotnet/checksums/{verRuntime}-sha.txt")
                );
                
                foreach(var os in supportedOSList) {
                    foreach(var arch in supportedArchList) {
                        var fileName = $"dotnet-sdk-{verSdk}-{os.osStr}-{arch.archStr}.{os.ext}";
                        var outDir = $"dotnet-{verRuntime}";

                        if(!shaMap.TryGetValue(fileName, out var sha512)) {
                            continue;
                        }

                        var sdkInfo = new SdkInfo(
                            implements: ListModule.OfArray(new[] { "dotnet" }),
                            version: verRuntime,
                            platforms: ListModule.OfArray(new[] {
                                new PlatformInfo(os.os, arch.arch), 
                            }),
                            setupSteps: ListModule.OfArray(new[] {
                                SdkSetupStep.NewDownload($"https://dotnetcli.azureedge.net/dotnet/Sdk/{verSdk}/{fileName}", fileName, SdkHash.NewSha512(sha512)),
                                SdkSetupStep.NewExtract(fileName, outDir),
                                SdkSetupStep.NewDelete(fileName), 
                            }),
                            
                            pathDirs: ListModule.OfArray(new[] { outDir }),
                            
                            env: MapModule.OfArray(new[] {
                                Tuple.Create("DOTNET_CLI_TELEMETRY_OPTOUT", EnvValue.NewOfString("1")),
                                Tuple.Create("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", EnvValue.NewOfString("1")),
                            }),
                            configFileTemplates: MapModule.OfArray(new[] {
                                Tuple.Create("$CONFIG/NuGet/NuGet.Config", configFile),
                                Tuple.Create("~/.nuget/NuGet/NuGet.Config", configFile), 
                            })
                        );

                        yield return ($"dotnet/{verRuntime}-{os.osStr}-{arch.archStr}.json", sdkInfo);
                    }
                }
            }
        }
    }
}