using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Docker.Registry.DotNet;
using Docker.Registry.DotNet.Authentication;
using Docker.Registry.DotNet.Models;
using dockerfile;
using Helium.Sdks;

namespace Helium.Engine.ContainerBuild
{
    public static class DockerfileResolver
    {

        public static async Task<(string resolvedDockerfile, Dictionary<string, string> imageMapping)> ProcessDockerfile(TextReader reader, PlatformInfo platform) {
            var dockerfile = await Dockerfile.ParseAsync(reader);
            var imageMapping = new Dictionary<string, string>();
            
            foreach(var inst in dockerfile.Instructions) {
                if(!inst.InstructionName.Equals("FROM", StringComparison.InvariantCultureIgnoreCase)) {
                    continue;
                }

                var requestedImage = inst.Arguments;
                if(requestedImage == null) {
                    throw new Exception("Invalid FROM statement.");
                }

                if(!imageMapping.TryGetValue(requestedImage, out var resolvedImage)) {
                    var (domain, image, digest) = await ResolveImageName(requestedImage, platform);
                    resolvedImage = $"{domain}/{image}@{digest}";
                    imageMapping.Add(requestedImage, resolvedImage);
                }
                    
                inst.Arguments = resolvedImage;
            }

            return (dockerfile.Contents(), imageMapping);
        }

        private const string defaultTag = "latest";
        
        private static async Task<(string domain, string image, string digest)> ResolveImageName(string imageName, PlatformInfo platform) {
            var (domain, remainder) = SplitDockerDomain(imageName);

            if(remainder.IndexOf('@') is var digestIndex && digestIndex >= 0) {
                var image = remainder.Substring(0, digestIndex);
                var digest = remainder.Substring(digestIndex + 1);
                ValidateImageName(image);
                ValidateDigest(digest);
                return (domain, image, digest);
            }
            else {
                var tagIndex = remainder.IndexOf(':');
                string image;
                string tag;
                if(tagIndex >= 0) {
                    image = remainder.Substring(0, tagIndex);
                    tag = remainder.Substring(tagIndex + 1);
                }
                else {
                    image = remainder;
                    tag = defaultTag;
                }
                
                ValidateImageName(image);
                ValidateTagName(tag);

                var digest = await LookupDigest(domain, image, tag, platform);

                const string sha256Prefix = "sha256:";
                if(!digest.StartsWith(sha256Prefix)) {
                    throw new Exception("Unexpected digest type.");
                }

                return (domain, image, digest.Substring(sha256Prefix.Length));
            }
            
            
        }

        private static async Task<string> LookupDigest(string domain, string image, string tag, PlatformInfo platform) {
            var configuration = new RegistryClientConfiguration(domain);

            using var client = configuration.CreateClient(new AnonymousOAuthAuthenticationProvider());

            await client.System.PingAsync();

            var manifestResult = await client.Manifest.GetManifestAsync(image, tag);

            return manifestResult.Manifest switch {
                ImageManifest2_1 _ => throw new Exception("Manifest version 2.1 does not have a digest."),
                ImageManifest2_2 manifest => manifest.Config.Digest,
                ManifestList manifestList =>
                    manifestList.Manifests
                        .FirstOrDefault(manifest => PlatformMatches(manifest.Platform, platform))
                        ?.Digest
                        ?? throw new Exception("Could not find manifest matching expected platform."),
                _ => throw new Exception("Unknown manifest type.")
            };
        }

        private static bool PlatformMatches(Platform manifestPlatform, PlatformInfo platform) =>
            OSMatches(manifestPlatform.Os, platform.os) && ArchMatches(manifestPlatform.Architecture, platform.arch);
        
        private static bool OSMatches(string manifestOS, SdkOperatingSystem os) =>
            (manifestOS, os) switch {
                ("linux", SdkOperatingSystem.Linux) => true,
                ("windows", SdkOperatingSystem.Windows) => true,
                _ => false,
            };
        
        private static bool ArchMatches(string manifestArch, SdkArch arch) =>
            (manifestArch, arch) switch {
                ("386", SdkArch.X86) => true,
                ("amd64", SdkArch.Amd64) => true,
                ("arm/v5", SdkArch.Arm) => true,
                ("arm/v7", SdkArch.Arm) => true,
                ("arm64/v8", SdkArch.Aarch64) => true,
                ("ppc64le", SdkArch.Ppc64le) => true,
                ("s390x", SdkArch.S390x) => true,
                _ => false,
            };

        private static void ValidateImageName(string image) {
            const string componentRegex = @"[a-z0-9]([a-z0-9]|\.[a-z0-9]|__?[a-z0-9]|\-+[a-z0-9])*";
            const string imageNameRegex = "^" + componentRegex + @"(\/" + componentRegex + ")*$";
            if(!Regex.IsMatch(image, imageNameRegex)) {
                throw new Exception("Invalid image name.");
            }
        }

        private static void ValidateTagName(string tag) {
            const string tagRegex = @"[a-zA-Z0-9_][a-zA-Z0-9_\-\.]*";
            if(!Regex.IsMatch(tag, tagRegex)) {
                throw new Exception("Invalid tag.");
            }
        }

        private static void ValidateDigest(string digest) {
            const string digestRegex = @"[a-f0-9]{64}";
            if(!Regex.IsMatch(digest, digestRegex)) {
                throw new Exception("Invalid digest.");
            }
        }

        private const string defaultDomain = "registry.hub.docker.com";
        private const string officialRepoName = "library";

        private static (string domain, string remainder) SplitDockerDomain(string imageName) {
            int i = imageName.IndexOf('/', StringComparison.InvariantCultureIgnoreCase);

            string domain;
            string remainder;
            if(i < 0 || (imageName.IndexOfAny(new[] {'.', ':'}, 0, i) < 0 && imageName.Substring(0, i) != "localhost")) {
                domain = defaultDomain;
                remainder = imageName;
            }
            else {
                domain = imageName.Substring(0, i);
                remainder = imageName.Substring(i + 1);
            }
            
            if(domain == defaultDomain && !remainder.Contains('/')) {
                remainder = officialRepoName + "/" + remainder;
            }

            return (domain, remainder);
        }

        private static void ValidateDomain(string domain) {
            const string domainRegex = @"^(([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9])\.)*([A-Za-z0-9]|[A-Za-z0-9][A-Za-z0-9\-]*[A-Za-z0-9])$";
            if(!Regex.IsMatch(domain, domainRegex)) {
                throw new Exception("Invalid domain.");
            }
        }
        
    }
}