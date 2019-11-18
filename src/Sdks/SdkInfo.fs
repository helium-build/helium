namespace Helium.Sdks

open Newtonsoft.Json
open System
open System.Runtime.InteropServices
open SemVer

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
    
type PlatformInfo =
    {
        os: SdkOperatingSystem;
        arch: SdkArch;
    }
    
    member this.SupportsRunning(execPlatform: PlatformInfo): bool =
        (execPlatform.os = SdkOperatingSystem.None || execPlatform.os = this.os) &&
            (execPlatform.arch = SdkArch.None || execPlatform.arch = this.arch)
    
    [<JsonIgnore>]
    member this.RootDirectory: string =
        match this.os with
        | SdkOperatingSystem.Linux -> "/"
        | SdkOperatingSystem.Windows -> "C:\\"
        | _ -> raise (new Exception("Unexpected OS"))
    
    static member Current: PlatformInfo = {
        os = if RuntimeInformation.IsOSPlatform OSPlatform.Windows then SdkOperatingSystem.Windows
             else if RuntimeInformation.IsOSPlatform OSPlatform.Linux then SdkOperatingSystem.Linux
             else raise (new PlatformNotSupportedException());
             
        arch = match RuntimeInformation.OSArchitecture with
               | Architecture.X86 -> SdkArch.X86
               | Architecture.X64 -> SdkArch.Amd64
               | Architecture.Arm -> SdkArch.Arm
               | Architecture.Arm64 -> SdkArch.Aarch64
               | _ -> raise (new PlatformNotSupportedException());
    }

[<RequireQualifiedAccess>]
type EnvValue =
    | OfString of string
    | Concat of EnvValue list
    | SdkDirectory
    
    member this.Resolve(sdkDirectory: string): string =
        match this with
        | EnvValue.OfString s -> s
        | EnvValue.Concat values -> values |> List.map (fun value -> value.Resolve sdkDirectory) |> String.Concat
        | EnvValue.SdkDirectory -> sdkDirectory
    
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
    
type SdkInfo =
    {
        implements: string list;
        version: string;
        platforms: PlatformInfo list;
        setupSteps: SdkSetupStep list;
        pathDirs: string list;
        env: Map<string, EnvValue>;
        configFileTemplates: Map<string, string>;
    }
    
    member this.Matches(name: string, versionRange: string): bool =
        (this.implements |> List.contains name) &&
            ((new Range(versionRange)).IsSatisfied this.version)
    
    member this.SupportedBy(platform: PlatformInfo): bool =
        this.platforms |> List.exists platform.SupportsRunning



