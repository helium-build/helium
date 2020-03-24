using System.Threading.Tasks;
using Helium.Sdks;

namespace Helium.Engine.Build.Cache
{
    public interface ISdkInstallManager
    {
        Task<(string hash, string installDir)> GetInstalledSdkDir(SdkInfo sdk);
    }
}