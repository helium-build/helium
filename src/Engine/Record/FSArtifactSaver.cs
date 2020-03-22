using System;
using System.IO;
using System.Threading.Tasks;
using Helium.Util;

namespace Helium.Engine.Record
{
    internal class FSArtifactSaver : IArtifactSaver
    {
        private readonly string outputDir;

        public FSArtifactSaver(string outputDir) {
            this.outputDir = outputDir;
        }

        public async Task SaveArtifact(string name, Stream stream) {
            if(name.Contains(Path.DirectorySeparatorChar) || name.Contains(Path.AltDirectorySeparatorChar)) {
                throw new ArgumentException("Invalid file name.", nameof(name));
            }

            await using var fileStream = File.Create(Path.Combine(outputDir, name));
            await stream.CopyToAsync(fileStream);
        }

        public async Task SaveArtifact(Stream stream, Func<string, Task<string>> nameSelector) {
            string tempFile;
            await using(var fileStream = FileUtil.CreateTempFile(outputDir, out tempFile)) {
                await stream.CopyToAsync(fileStream);
            }

            var name = await nameSelector(tempFile);
            
            if(name.Contains(Path.DirectorySeparatorChar) || name.Contains(Path.AltDirectorySeparatorChar)) {
                throw new ArgumentException("Invalid file name.", nameof(name));
            }
            
            File.Move(tempFile, Path.Combine(outputDir, name), overwrite: true);
        }
    }
}