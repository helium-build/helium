using System.Threading;
using System.Threading.Tasks;
using Helium.Sdks;

namespace Helium.CI.Server
{
    public delegate Task<bool> PlatformChecker(PlatformInfo platform, CancellationToken cancellationToken);
}