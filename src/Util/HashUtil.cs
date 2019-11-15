using System.Text.RegularExpressions;

namespace Helium.Util
{
    public static class HashUtil
    {
        private static readonly Regex sha256 = new Regex(@"^(?<hash>[a-fA-F0-9]{64})\s+.");

        public static string? ParseSha256(string input) {
            var match = sha256.Match(input);
            return match.Success ? match.Groups["hash"].Value : null;
        }
        
    }
}