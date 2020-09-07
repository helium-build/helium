using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Helium.Sdks
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum SdkArch
    {
        None,
        X86,
        Amd64,
        Arm,
        Aarch64,
        Ppc64le,
        S390x,
    }
}