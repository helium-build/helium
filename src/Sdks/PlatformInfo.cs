using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Helium.Sdks
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum SdkOperatingSystem {
        None,
        Linux,
        Windows,
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum SdkArch {
        None,
        X86,
        Amd64,
        Arm,
        Aarch64,
        Ppc64le,
        S390x,
    }

    public class PlatformInfo
    {
        [JsonConstructor]
        public PlatformInfo(SdkOperatingSystem os, SdkArch arch) {
            OS = os;
            Arch = arch;
        }
        
        public PlatformInfo(IDictionary<string, object> obj)
        : this(
            os: Enum.Parse<SdkOperatingSystem>((string)obj["os"]),
            arch: Enum.Parse<SdkArch>((string)obj["arch"])
        )
        {}

        public SdkOperatingSystem OS { get; }
        public SdkArch Arch { get; }

        public bool SupportsRunning(PlatformInfo execPlatform) =>
            (execPlatform.OS == SdkOperatingSystem.None || execPlatform.OS == OS) &&
                (execPlatform.Arch == SdkArch.None || execPlatform.Arch == Arch);

        [JsonIgnore]
        public string RootDirectory =>
            OS switch {
                SdkOperatingSystem.Linux => "/",
                SdkOperatingSystem.Windows => "C:\\",
                _ => throw new Exception("Unexpected OS")
            };
        
        public static PlatformInfo Current => new PlatformInfo(
            os:
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? SdkOperatingSystem.Windows
                : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? SdkOperatingSystem.Linux
                : throw new PlatformNotSupportedException(),
            arch: RuntimeInformation.OSArchitecture switch {
                Architecture.X86 => SdkArch.X86,
                Architecture.X64 => SdkArch.Amd64,
                Architecture.Arm => SdkArch.Arm,
                Architecture.Arm64 => SdkArch.Aarch64,
                _ => throw new PlatformNotSupportedException(),
            }
        );

    }
}
