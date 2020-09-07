using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Helium.Util;
using Newtonsoft.Json;

namespace Helium.Sdks
{
    public static class SdkLoader
    {
        private static JsonSerializerSettings SerSettings => new JsonSerializerSettings {
            Formatting = Formatting.Indented,
        };

        public static async Task<SdkInfo> LoadSdk(string sdkFile) {
            var data = await File.ReadAllTextAsync(sdkFile);
            return JsonConvert.DeserializeObject<SdkInfo>(data, SerSettings) ?? throw new Exception("Could not load sdk.");
        }

        public static async IAsyncEnumerable<SdkInfo> LoadSdks(string dir) {
            foreach(var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories)) {
                yield return await LoadSdk(file);
            }
        }

        public static Task SaveSdk(string sdkFile, SdkInfo sdk) =>
            File.WriteAllTextAsync(sdkFile, JsonConvert.SerializeObject(sdk, typeof(SdkInfo), SerSettings));

        public static string SdkSha256(SdkInfo sdk) {
            var settings = SerSettings;
            settings.Formatting = Formatting.None;
            var json = JsonConvert.SerializeObject(sdk, typeof(SdkInfo), settings);
            return HashUtil.Sha256UTF8(json);
        }
    }
}