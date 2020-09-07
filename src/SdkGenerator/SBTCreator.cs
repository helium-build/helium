using System;
using System.Collections.Generic;
using Helium.Sdks;

namespace Helium.SdkGenerator
{
    public class SBTCreator : ISdkCreator
    {

        private const string version = "1.3.3";

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
                    new SdkSetupStep.Download($"https://piccolo.link/sbt-{version}.tgz", fileName, new SdkHash(SdkHashType.Sha256, "fe64a24ecd26ae02ac455336f664bbd7db6a040144b3106f1c45ebd42e8a476c")),
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