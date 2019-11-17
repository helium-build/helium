using System;
using System.Threading.Tasks;

namespace Helium.Engine.Record
{
    internal class ArchiveRecorder : IRecorder
    {
        public ArchiveRecorder(string cacheDir, string sdkDir, string schemaFile, string archiveFile, string sourcesDir, string confDir) {
            throw new NotImplementedException();
        }

        public static Task<IRecorder> Create(string cacheDir, string sdkDir, string schemaFile, string archiveFile, string sourcesDir, string confDir) {
            throw new NotImplementedException();
        }

        public ValueTask DisposeAsync() {
            throw new NotImplementedException();
        }
    }
}