using System;
using System.IO;
using System.Threading.Tasks;

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

        public Task SaveArtifact(string name, Func<string, Task<string>> nameSelector) {
            throw new NotImplementedException();
        }
    }
}