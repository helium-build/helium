using System;

namespace Helium.Sdks
{
    public enum SdkOperatingSystem {
        Any,
        Linux,
        Windows,
    }

    public enum SdkArch {
        Any,
        Amd64,
        Arm,
        Aarch64,
        Ppc64le,
        S390x,
    }

    public class PlatformInfo
    {
        public PlatformInfo(SdkOperatingSystem os, SdkArch arch) {
            OS = os;
            Arch = arch;
        }

        public SdkOperatingSystem OS { get; }
        public SdkArch Arch { get; }
    }
}
