using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Helium.Sdks;
using Helium.Util;
using Microsoft.FSharp.Collections;

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

            var os = SdkHelper.parseOperatingSystem(binary.os);
            var arch = SdkHelper.parseArch(binary.architecture);

            var zipDirName =
                binary.version == "10"
                    ? release.release_name.Replace(binary.version_data.openjdk_version, binary.version_data.semver)
                    : release.release_name;

            var sdkInfo = new SdkInfo(
                implements: ListModule.OfArray(new[] {"jdk"}),
                version: binary.version_data.semver,
                platforms: ListModule.OfArray(new[] {new PlatformInfo(os, arch)}),
                setupSteps: ListModule.OfArray(new[] {
                    SdkSetupStep.NewDownload(binary.binary_link, binary.binary_name, SdkHash.NewSha256(sha256)),
                    SdkSetupStep.NewExtract(binary.binary_name, "."),
                    SdkSetupStep.NewDelete(binary.binary_name),
                }),
                pathDirs: ListModule.OfArray(new[] {
                    zipDirName + "/bin"
                }),
                env: MapModule.OfArray(new[] {
                    Tuple.Create("JAVA_HOME", EnvValue.NewConcat(
                        ListModule.OfArray(new[] {
                            EnvValue.SdkDirectory,
                            EnvValue.NewOfString("/" + zipDirName),
                        })
                    )),
                }),
                configFileTemplates: MapModule.Empty<string, string>()
            );
            
            return ($"jdk/jdk{binary.version}/{Path.GetFileNameWithoutExtension(binary.binary_name)}.json", sdkInfo);
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
                var release = await GetAdoptOpenJDKRelease(url);
                foreach(var binary in release.binaries) {
                    yield return await GetSDKForBinary(release, binary);
                }
            }
        }
    }
}