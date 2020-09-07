using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Helium.Sdks;
using Helium.Util;

namespace Helium.SdkGenerator
{
    public class NodeJSCreator : ISdkCreator
    {
        public string Name => "node";

        private readonly string[] versions = {
            "12.7.0",
        };

        private readonly (SdkOperatingSystem os, string osStr, SdkArch arch, string archStr, string ext)[] platforms = {
            (SdkOperatingSystem.Windows, "win", SdkArch.Amd64, "x64", "zip"),
            (SdkOperatingSystem.Windows, "win", SdkArch.X86, "x86", "zip"),
            (SdkOperatingSystem.Linux, "linux", SdkArch.Amd64, "x64", "tar.gz"),
            (SdkOperatingSystem.Linux, "linux", SdkArch.Arm, "armv7l", "tar.gz"),
            (SdkOperatingSystem.Linux, "linux", SdkArch.Aarch64, "arm64", "tar.gz"),
            (SdkOperatingSystem.Linux, "linux", SdkArch.Ppc64le, "ppc64le", "tar.gz"),
            (SdkOperatingSystem.Linux, "linux", SdkArch.S390x, "s390x", "tar.gz"),
        };


        private const string configFile =
@"
{% if repos.npm != null -%}
registry={{repos.npm.registry}}
{% endif -%}
";
        
        
        
        public async IAsyncEnumerable<(string path, SdkInfo)> GenerateSdks() {
            foreach(var version in versions) {

                var shaMap = HashUtil.ParseSha256File(
                    await HttpUtil.FetchString($"https://nodejs.org/dist/v{version}/SHASUMS256.txt")
                );
                
                foreach(var platform in platforms) {
                    var archiveDir = $"node-v{version}-{platform.osStr}-{platform.archStr}";
                    var fileName = $"{archiveDir}.{platform.ext}";
                    
                    

                    if(!shaMap.TryGetValue(fileName, out var sha256)) {
                        continue;
                    }

                    var sdkInfo = new SdkInfo(
                        implements: new[] { "node" },
                        version: version,
                        platforms: new[] {
                            new PlatformInfo(platform.os, platform.arch), 
                        },
                        setupSteps: new SdkSetupStep[] {
                            new SdkSetupStep.Download($"https://nodejs.org/dist/v{version}/{fileName}", fileName, new SdkHash(SdkHashType.Sha256, sha256)),
                            new SdkSetupStep.Extract(fileName, "."),
                            new SdkSetupStep.Delete(fileName), 
                        },
                            
                        pathDirs: new[] { archiveDir + "/bin" },
                        
                        configFileTemplates: new Dictionary<string, string> {
                            { "~/.npmrc", configFile }, 
                        }
                    );

                    yield return ($"nodejs/v{version}-{platform.osStr}-{platform.archStr}.json", sdkInfo);
                }
            }
        }
    }
}