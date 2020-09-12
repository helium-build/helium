using System;
using Helium.Sdks;

namespace SdkServer
{
    public interface IPlatformSdk
    {
        SdkInfo SdkForBaseUrl(Uri baseUrl);

        string? UrlForFile(int fileId);
    }
}