using System.Collections.Generic;
using Helium.Sdks;

namespace SdkServer
{
    public interface IVersionedSdk
    {
        IEnumerable<PlatformInfo> Platforms { get; }
        IPlatformSdk? GetPlatform(PlatformInfo platform);
    }
}