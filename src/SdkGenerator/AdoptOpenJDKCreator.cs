using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Helium.Sdks;
using Helium.Util;

namespace Helium.SdkGenerator
{
    internal class AdoptOpenJDKCreator : ISdkCreator
    {
        public string Name => "AdoptOpenJDK";

        private sealed class OpenJDKRelease
        {
            public string? release_name { get; set; }
            public string? release_link { get; set; }
            public string? timestamp { get; set; }
            public List<OpenJDKBinary> binaries { get; set; } = new List<OpenJDKBinary>();
        }

        private sealed class OpenJDKBinary
        {
            public string? os { get; set; }
            public string? architecture { get; set; }
            public string? binary_name { get; set; }
            public string? binary_link { get; set; }
            public string? checksum_link { get; set; }
            public string? version { get; set; }
            public BinVersion? version_data { get; set; }
        }
        
        private sealed class BinVersion
        {
            public string? openjdk_version { get; set; }
            public string? semver { get; set; }
        }

        private Task<OpenJDKRelease> GetAdoptOpenJDKRelease(string url) =>
            HttpUtil.FetchJson<OpenJDKRelease>(url);

        private async Task<(string path, SdkInfo)> GetSDKForBinary(OpenJDKRelease release, OpenJDKBinary binary) {

            if(
                release.release_name == null ||
                release.release_link == null ||
                binary.os == null ||
                binary.architecture == null ||
                binary.binary_name == null ||
                binary.binary_link == null ||
                binary.checksum_link == null ||
                binary.version_data == null ||
                binary.version_data.openjdk_version == null ||
                binary.version_data.semver == null
            ) {
                throw new ArgumentException("Property is null");
            }
            
            Console.WriteLine($"Creating SDK for binary {binary.binary_name}");

            var shaFileContent = await HttpUtil.FetchString(binary.checksum_link);
            var sha256 = HashUtil.ParseSha256(shaFileContent) ?? throw new Exception($"Invalid SHA256 from {binary.checksum_link}.");

            var os = SdkHelper.ParseOperatingSystem(binary.os);
            var arch = SdkHelper.ParseArch(binary.architecture);

            var zipDirName =
                binary.version == "10"
                    ? release.release_name.Replace(binary.version_data.openjdk_version, binary.version_data.semver)
                    : release.release_name;

            var sdkInfo = new SdkInfo(
                implements: new[] {"jdk"},
                version: binary.version_data.semver,
                platforms: new[] {new PlatformInfo(os, arch)},
                setupSteps: new SdkSetupStep[] {
                    new SdkSetupStep.Download(binary.binary_link, binary.binary_name, new SdkHash(SdkHashType.Sha256, sha256)),
                    new SdkSetupStep.Extract(binary.binary_name, "."),
                    new SdkSetupStep.Delete(binary.binary_name),
                },
                pathDirs: new[] {
                    zipDirName + "/bin"
                },
                env: new Dictionary<string, EnvValue> {
                    { "JAVA_HOME", new EnvValue.Concat(
                        new EnvValue[] {
                            new EnvValue.BuiltInValue(EnvValue.BuiltInValue.SdkDirectory),
                            new EnvValue.OfString("/" + zipDirName),
                        }
                    ) },
                }
            );

            var noExtFile = Path.GetFileNameWithoutExtension(binary.binary_name);
            if(noExtFile.EndsWith(".tar")) {
                noExtFile = Path.GetFileNameWithoutExtension(noExtFile);
            }
            
            return ($"jdk/jdk{binary.version}/{noExtFile}.json", sdkInfo);
        }

        private IEnumerable<string> Urls() {
            foreach(var ver in new int[] { 8, 9, 10, 11, 12, 13 }) {
                foreach(var os in new[] { "linux", "windows" }) {
                    
                    
                    IEnumerable<string> GetArch() {
                        switch(os) {
                            case "linux":
                                yield return "x64";
                                yield return "aarch64";
                                yield return "ppc64le";
                                yield return "s390x";
                                if(ver == 8 || ver >= 11) yield return "arm";
                                break;
                            
                            case "windows":
                                yield return "x64";
                                if(ver == 8 || ver >= 11) yield return "x32";
                                break;
                            
                            default:
                                break;
                        }
                    }
                    
                    foreach(var arch in GetArch()) {
                        yield return $"https://api.adoptopenjdk.net/v2/info/releases/openjdk{ver}?openjdk_impl=hotspot&release=latest&type=jdk&os={os}&arch={arch}";
                    }
                }
            }
        }
        
        public async IAsyncEnumerable<(string path, SdkInfo)> GenerateSdks() {
            foreach(var url in Urls()) {
                Console.WriteLine($"Checking AdoptOpenJDK release: {url}");
                var release = await GetAdoptOpenJDKRelease(url);
                foreach(var binary in release.binaries) {
                    (string path, SdkInfo) sdk;
                    try {
                        sdk = await GetSDKForBinary(release, binary);
                    }
                    catch(WebException) {
                        Console.WriteLine("Could not generate SDK.");
                        continue;
                    }
                    
                    yield return sdk;
                }
            }
        }
    }
}