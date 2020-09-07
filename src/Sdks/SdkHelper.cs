using System;

namespace Helium.Sdks
{
    public static class SdkHelper
    {
        public static SdkOperatingSystem ParseOperatingSystem(string os) {

            bool Check(string name) => os.Equals(name, StringComparison.InvariantCultureIgnoreCase);
            
            
            if(Check("linux")) return SdkOperatingSystem.Linux;
            else if(Check("windows") || Check("win") || Check("win32")) return SdkOperatingSystem.Windows;
            else throw new UnknownPlatformException();
        }

        public static SdkArch ParseArch(string arch) {

            bool Check(string name) => arch.Equals(name, StringComparison.InvariantCultureIgnoreCase);
            
            
                if(Check("x64") || Check("x86_64") || Check("amd64")) return SdkArch.Amd64;
                else if(Check("x86") || Check("x32")) return SdkArch.X86;
                else if(Check("arm") || Check("arm32")) return SdkArch.Arm;
                else if(Check("arm64") || Check("aarch64")) return SdkArch.Aarch64;
                else if(Check("ppc64le")) return SdkArch.Ppc64le;
                else if(Check("s390x")) return SdkArch.S390x;
                else throw new UnknownPlatformException();
            
        }
    }
}