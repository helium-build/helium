using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Newtonsoft.Json;

namespace Helium.Sdks
{
    public class PlatformInfo: IEquatable<PlatformInfo>
    {
        public PlatformInfo(SdkOperatingSystem os, SdkArch arch) {
            OS = os;
            Arch = arch;
        }

        public PlatformInfo(IDictionary<string, object?> dict) 
            : this(
                os: (SdkOperatingSystem)Convert.ToInt32(dict["os"]),
                arch: (SdkArch)Convert.ToInt32(dict["arch"])
            ) {}
        
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
                _ => throw new Exception("Unexpected OS"),
            };

        public static PlatformInfo Current {
            get {
                SdkOperatingSystem os;
                if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) os = SdkOperatingSystem.Windows;
                else if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) os = SdkOperatingSystem.Linux;
                else throw new PlatformNotSupportedException();

                SdkArch arch = RuntimeInformation.OSArchitecture switch {
                    Architecture.X86 => SdkArch.X86,
                    Architecture.X64 => SdkArch.Amd64,
                    Architecture.Arm => SdkArch.Arm,
                    Architecture.Arm64 => SdkArch.Aarch64,
                    _ => throw new PlatformNotSupportedException(),
                };
                
                return new PlatformInfo(os, arch);
            }
        }

        public bool Equals(PlatformInfo other) =>
            other != null &&
            OS == other.OS &&
            Arch == other.Arch;

        public override bool Equals(object obj) =>
            obj is PlatformInfo other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(OS.GetHashCode(), Arch.GetHashCode());

        public override string ToString() =>
            $"PlatformInfo(os={OS}, arch={Arch})";
    }
}