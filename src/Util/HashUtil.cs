using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Helium.Util
{
    public static class HashUtil
    {
        private static readonly Regex sha256 = new Regex(@"^(?<hash>[a-fA-F0-9]{64})\s+.");
        private static readonly Regex sha256File = new Regex(@"^(?<hash>[a-fA-F0-9]{64})\s+(?<fileName>.+)");
        private static readonly Regex sha512File = new Regex(@"^(?<hash>[a-fA-F0-9]{128})\s+(?<fileName>.+)");

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
        
        private static string HashToHex(byte[] hash) =>
            string.Concat(hash.Select(b => b.ToString("x2")));

        public static string Sha256UTF8(string str) {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(str));
            return HashToHex(hash);
        }

        public static async Task<bool> ValidateSha256(Stream stream, string expected) {
            using var sha256 = SHA256.Create();
            var hash = await HashStream(sha256, stream);
            return string.Compare(hash, expected, StringComparison.InvariantCultureIgnoreCase) == 0;
        }

        public static async Task<bool> ValidateSha512(Stream stream, string expected) {
            using var sha512 = SHA512.Create();
            var hash = await HashStream(sha512, stream);
            return string.Compare(hash, expected, StringComparison.InvariantCultureIgnoreCase) == 0;
        }

        private static async Task<string> HashStream(HashAlgorithm hash, Stream stream) {
            var buffer = new byte[8192];

            int bytesRead;
            while((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0) {
                hash.TransformBlock(buffer, 0, bytesRead, null, 0);
            }

            hash.TransformFinalBlock(buffer, 0, 0);

            return HashToHex(hash.Hash);
        }
    }
}