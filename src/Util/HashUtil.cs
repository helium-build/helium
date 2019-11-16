using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

namespace Helium.Util
{
    public static class HashUtil
    {
        private static readonly Regex sha256 = new Regex(@"^(?<hash>[a-fA-F0-9]{64})\s+.");
        private static readonly Regex sha256File = new Regex(@"^(?<hash>[a-fA-F0-9]{64})\s+.(<fileName>.+)");
        private static readonly Regex sha512File = new Regex(@"^(?<hash>[a-fA-F0-9]{128})\s+.(<fileName>.+)");

        public static string? ParseSha256(string input) {
            var match = sha256.Match(input);
            return match.Success ? match.Groups["hash"].Value : null;
        }

        public static Dictionary<string, string> ParseSha256File(string input) =>
            input
                .Split(new[] {'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => sha256File.Match(line))
                .Where(match => match.Success)
                .ToDictionary(
                    match => match.Groups["fileName"].Value,
                    match => match.Groups["hash"].Value
                );

        public static Dictionary<string, string> ParseSha512File(string input) =>
            input
                .Split(new[] {'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => sha512File.Match(line))
                .Where(match => match.Success)
                .ToDictionary(
                    match => match.Groups["fileName"].Value,
                    match => match.Groups["hash"].Value
                );
    }
}