using System.Collections.Generic;
using Helium.Sdks;

namespace Helium.SdkGenerator
{
    public class DotNetMergeCreator : ISdkCreator
    {
        private const string shellScript =
            @"#!/bin/bash

if [ ! -d /sdk/dotnet-sdk ]; then
  mkdir /sdk/dotnet-sdk
  IFS=:
  for dir in $PATH; do
    if [ ""$dir"" != """" ] && [ ""$dir"" != ""$(dirname ""$0"")"" ] && [ -f ""$dir/dotnet"" ]; then
      cp -n ""$dir/dotnet"" /sdk/dotnet-sdk/
      cp -rnsT ""$dir/"" /sdk/dotnet-sdk
    fi
  done
fi

exec /sdk/dotnet-sdk/dotnet ""$@""
";
        
        
        public string Name => "dotnet-merge";
        
        public async IAsyncEnumerable<(string path, SdkInfo)> GenerateSdks() {
            
            var sdkInfo = new SdkInfo(
                implements: new[] { "dotnet-merge" },
                version: "1.0.0",
                platforms: new[] {
                    new PlatformInfo(SdkOperatingSystem.Linux, SdkArch.None), 
                },
                
                setupSteps: new SdkSetupStep[] {
                   new SdkSetupStep.CreateDirectory("dotnet-merge/"),
                   new SdkSetupStep.CreateFile("dotnet-merge/dotnet", true, shellScript),
                },
                
                pathDirs: new[] { "dotnet-merge" }
            );

            yield return ("dotnet-merge/dotnet-merge-1.0.0-linux.json", sdkInfo);
        }
    }
}