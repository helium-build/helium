module Helium.Sdks.SdkLoader
open Newtonsoft.Json
open System.IO
open System.Threading.Tasks
open FSharp.Control.Tasks.V2


let private createSerSettings () =
    let settings = JsonSerializerSettings()
    settings.Converters.Add <| Converters.StringEnumConverter()
    settings

let loadSdk (sdkFile: string): SdkInfo Task = task {
    let! data = File.ReadAllTextAsync(sdkFile)
    return JsonConvert.DeserializeObject<SdkInfo>(data, createSerSettings())
}

let private findFiles dir =
    Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories)

let loadSdks =
    findFiles >> Seq.map loadSdk >> Task.WhenAll
    
let saveSdk (sdk: SdkInfo) (path: string): Task =
    File.WriteAllTextAsync(path, JsonConvert.SerializeObject(sdk, typeof<SdkInfo>, createSerSettings()))
