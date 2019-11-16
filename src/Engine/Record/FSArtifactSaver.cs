using System;
using System.IO;
using System.Threading.Tasks;

namespace Helium.Engine.Record
{
    internal class FSArtifactSaver : IArtifactSaver
    {
        public FSArtifactSaver(string outputDir) {
            throw new NotImplementedException();
        }

        public Task SaveArtifact(string name, Stream stream) {
            throw new NotImplementedException();
        }

        public Task SaveArtifact(string name, Func<string, Task<string>> nameSelector) {
            throw new NotImplementedException();
        }
    }
}