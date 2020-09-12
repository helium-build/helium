using System.Collections.Generic;

namespace SdkServer
{
    public interface ISdkPackage
    {
        IEnumerable<string> Versions { get; }
        IVersionedSdk? GetVersion(string version);
    }
}