using System;
using System.Collections.Generic;
using Helium.Sdks;
using Microsoft.FSharp.Collections;

namespace Helium.SdkGenerator
{
    public class SBTCreator : ISdkCreator
    {

        private const string version = "1.3.2";

        private const string repoTemplate =
@"
[repositories]
{{ for repo in repos.maven }}
{{ repo.name }}: {{ repo.url }}
{{ endfor }}
";
        
        public string Name => "SBT";

        public async IAsyncEnumerable<(string, SdkInfo)> GenerateSdks() {
            var fileName = $"sbt-{version}.tgz";
            
            var sdk = new SdkInfo(
                implements: ListModule.OfArray(new[] { "sbt" }),
                version: version,
                platforms: ListModule.OfArray(new[] {
                    new PlatformInfo(SdkOperatingSystem.None, SdkArch.None), 
                }),
                setupSteps: ListModule.OfArray(new[] {
                    SdkSetupStep.NewDownload($"https://piccolo.link/sbt-{version}.tgz", fileName, SdkHash.NewSha256("ed8cef399129895ad0d757eea812b3f95830a36fa838f8ede1c6cdc2294f326f")),
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