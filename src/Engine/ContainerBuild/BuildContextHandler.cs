using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Helium.Util;
using ICSharpCode.SharpZipLib.Tar;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Helium.Engine.Docker
{
    public static class BuildContextHandler
    {

        private static async IAsyncEnumerable<string> Lines(TextReader reader) {
            string? line;
            while((line = await reader.ReadLineAsync()) != null) {
                yield return line;
            }
        }

        private delegate void IgnoreHandler(string path, ref bool ignored);

        private static IAsyncEnumerable<IgnoreHandler> IgnoreHandlers(TextReader reader) =>
            Lines(reader)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .Select(line => {
                    IgnoreHandler handler;
                    if(line.StartsWith("!")) {
                        var matcher = new Matcher(StringComparison.Ordinal);
                        matcher.AddInclude(line.Substring(1));
                        
                        handler = (string path, ref bool ignored) => {
                            if(!ignored) return;
                            ignored = !matcher.Match(path).HasMatches;
                        };
                    }
                    else {
                        var matcher = new Matcher(StringComparison.Ordinal);
                        matcher.AddInclude(line);
                        
                        handler = (string path, ref bool ignored) => {
                            if(ignored) return;
                            ignored = matcher.Match(path).HasMatches;
                        };
                    }

                    return handler;
                });

        private static async Task<Func<string, bool>> CreateIgnorePredicate(TextReader reader) {
            var handlers = await IgnoreHandlers(reader).ToListAsync();

            return path => {
                bool ignored = false;
                foreach(var handler in handlers) {
                    handler(path, ref ignored);
                }

                return !ignored;
            };
        }

        
        private static Func<string, bool> ExcludeDockerfile(Func<string, bool> filter) => path =>
            path != "Dockerfile" && filter(path);


        public static async Task WriteBuildContext(string directory, string dockerfileContent, Stream stream) {
            await using var tarStream = new TarOutputStream(stream);

            var dockerIgnorePath = Path.Combine(directory, ".dockerignore");
            Func<string, bool> filter;
            if(File.Exists(dockerIgnorePath)) {
                using var ignoreReader = File.OpenText(dockerIgnorePath);
                filter = await CreateIgnorePredicate(ignoreReader);
            }
            else {
                filter = _ => true;
            }

            await ArchiveUtil.AddStringToTar(tarStream, "Dockerfile", dockerfileContent);
            
            filter = ExcludeDockerfile(filter);

            await ArchiveUtil.AddDirToTar(tarStream, "", directory, filter);
        }
    }
}