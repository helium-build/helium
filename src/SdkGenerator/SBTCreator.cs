using System;
using System.Collections.Generic;
using Helium.Sdks;

namespace Helium.SdkGenerator
{
    public class SBTCreator : ISdkCreator
    {

        private const string version = "1.4.0";

        private const string repoTemplate =
@"
[repositories]
{% for repo in repos.maven -%}
{{ repo.name }}: {{ repo.url }}
{% endfor -%}
";
        
        public string Name => "SBT";

        public async IAsyncEnumerable<(string path, SdkInfo)> GenerateSdks() {
            var fileName = $"sbt-{version}.tgz";
            
            var sdk = new SdkInfo(
                implements: new[] { "sbt" },
                version: version,
                platforms: new[] {
                    new PlatformInfo(SdkOperatingSystem.None, SdkArch.None), 
                },
                setupSteps: new SdkSetupStep[] {
                    new SdkSetupStep.Download($"https://github.com/sbt/sbt/releases/download/v{version}/sbt-{version}.tgz", fileName, new SdkHash(SdkHashType.Sha256, "b4775b470920e03de7a5d81121b4dc741c00513f041e65dbb981052ec6d1eed5")),
                    new SdkSetupStep.Extract(fileName, "."),
                    new SdkSetupStep.Delete(fileName), 
                },
                pathDirs: new[] { "sbt/bin" },
                configFileTemplates: new Dictionary<string, string> {
                    { "~/.sbt/repositories", repoTemplate }, 
                }
            );

            yield return ($"sbt/sbt-{version}.json", sdk);
        }
    }
}