using System.Collections;

namespace SdkServer
{
    public interface ISdkResolver
    {
        ISdkPackage? GetSdk(string name);
    }
}