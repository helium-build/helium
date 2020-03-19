using System.IO;
using System.Linq;

namespace Helium.Util
{
    public static class PathUtil
    {
        public static bool IsValidSubPath(string path) =>
            !Path.IsPathRooted(path) && path.Split('/', '\\').All(seg => seg != "..");
    }
}