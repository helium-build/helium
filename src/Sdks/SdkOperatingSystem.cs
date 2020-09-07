using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Helium.Sdks
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum SdkOperatingSystem
    {
        None,
        Linux,
        Windows,
    }
}