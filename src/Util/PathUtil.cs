using System.Linq;

namespace Helium.Util
{
    public static class PathUtil
    {
        public static bool IsValidSubPath(string path) =>
            !path.StartsWith("/") && !path.Split('/', '\\').Any(seg => seg == "." || seg == "..");
    }
}