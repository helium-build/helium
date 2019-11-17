using System.Threading.Tasks;
using Helium.Sdks;

namespace Helium.Engine.Cache
{
    public class SdkInstallManager
    {
        public Task<(string hash, string installDir)> GetInstalledSdkDir(SdkInfo sdk) {
            throw new System.NotImplementedException();
        }
    }
}