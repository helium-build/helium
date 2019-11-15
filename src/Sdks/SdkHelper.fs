module Helium.Sdks.SdkHelper

open System


exception UnknownPlatformException

let private (|IgnoreCase|_|) (s1: string) (s2: string) =
    if String.Compare(s1, s2, StringComparison.InvariantCultureIgnoreCase) = 0 then
        Some()
    else
        None

let parseOperatingSystem: string -> SdkOperatingSystem = function
    | IgnoreCase "linux" -> SdkOperatingSystem.Linux
    | IgnoreCase "windows" | IgnoreCase "win" | IgnoreCase "win32" -> SdkOperatingSystem.Windows
    | _ -> raise UnknownPlatformException
    
    
let parseArch: string -> SdkArch = function
    | IgnoreCase "x64" | IgnoreCase "x86_64" | IgnoreCase "amd64" -> SdkArch.Amd64
    | IgnoreCase "x86" | IgnoreCase "x32" -> SdkArch.X86
    | IgnoreCase "arm" | IgnoreCase "arm32" -> SdkArch.Arm
    | IgnoreCase "arm64" | IgnoreCase "aarch64" -> SdkArch.Aarch64
    | IgnoreCase "ppc64le" -> SdkArch.Ppc64le
    | IgnoreCase "s390x" -> SdkArch.S390x
    | _ -> raise UnknownPlatformException
    
