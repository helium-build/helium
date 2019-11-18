module Helium.Sdks.SdkLoader
open Newtonsoft.Json
open System.IO
open System.Threading.Tasks
open FSharp.Control.Tasks.V2
open Helium.Util


let private createSerSettings () =
    let settings = JsonSerializerSettings()
    settings.Converters.Add <| Converters.StringEnumConverter()
    settings.Formatting <- Formatting.Indented
    settings

let loadSdk (sdkFile: string): SdkInfo Task = task {
    let! data = File.ReadAllTextAsync(sdkFile)
    return JsonConvert.DeserializeObject<SdkInfo>(data, createSerSettings())
}

let private findFiles dir =
    Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories)

let loadSdks =
    findFiles >> Seq.map loadSdk
    
let saveSdk (sdk: SdkInfo) (path: string): Task =
    File.WriteAllTextAsync(path, JsonConvert.SerializeObject(sdk, typeof<SdkInfo>, createSerSettings()))
    
let sdkSha256 (sdk: SdkInfo): string =
    let settings = createSerSettings()
    settings.Formatting <- Formatting.None
    JsonConvert.SerializeObject(sdk, typeof<SdkInfo>, settings) |> HashUtil.Sha256UTF8
    
