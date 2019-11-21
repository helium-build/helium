using System;
using System.Collections.Generic;
using Helium.Sdks;
using Microsoft.FSharp.Collections;

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
                implements: ListModule.OfArray(new[] { "sbt" }),
                version: version,
                platforms: ListModule.OfArray(new[] {
                    new PlatformInfo(SdkOperatingSystem.None, SdkArch.None), 
                }),
                setupSteps: ListModule.OfArray(new[] {
                    SdkSetupStep.NewDownload($"https://piccolo.link/sbt-{version}.tgz", fileName, SdkHash.NewSha256("fe64a24ecd26ae02ac455336f664bbd7db6a040144b3106f1c45ebd42e8a476c")),
                    SdkSetupStep.NewExtract(fileName, "."),
                    SdkSetupStep.NewDelete(fileName), 
                }),
                pathDirs: ListModule.OfArray(new[] { "sbt/bin" }),
                env: MapModule.Empty<string, EnvValue>(),
                configFileTemplates: MapModule.OfArray(new[] {
                    Tuple.Create("~/.sbt/repositories", repoTemplate), 
                })
            );

            yield return ($"sbt/sbt-{version}.json", sdk);
        }
    }
}