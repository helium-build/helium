namespace Helium.Sdks

[<RequireQualifiedAccess>]
type SdkOperatingSystem =
    | None = 0
    | Linux = 1
    | Windows = 2
    
[<RequireQualifiedAccess>]
type SdkArch =
    | None = 0
    | X86 = 1
    | Amd64 = 2
    | Arm = 3
    | Aarch64 = 4
    | Ppc64le = 5
    | S390x = 6
    
type PlatformInfo = {
    os: SdkOperatingSystem;
    arch: SdkArch;
}

[<RequireQualifiedAccess>]
type EnvValue =
    | OfString of string
    | Concat of EnvValue list
    | SdkDirectory
    
[<RequireQualifiedAccess>]
type SdkHash =
    | Sha256 of string
    | Sha512 of string


[<RequireQualifiedAccess>]
type SdkSetupStep =
    | Download of url: string * fileName: string * SdkHash
    | Extract of fileName: string * directory: string
    | Delete of fileName: string
    | CreateDirectory of fileName: string
    | CreateFile of fileName: string * isExecutable: bool * content: string
    
type SdkInfo = {
    implements: string list;
    version: string;
    platforms: PlatformInfo list;
    setupSteps: SdkSetupStep list;
    pathDirs: string list;
    env: Map<string, EnvValue>;
    configFileTemplates: Map<string, string>;
}



