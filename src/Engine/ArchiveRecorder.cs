using System;
using System.Threading.Tasks;
using Helium.Engine.Record;

namespace Helium.Engine
{
    internal class ArchiveRecorder : IRecorder
    {
        public ArchiveRecorder(string cacheDir, string sdkDir, string schemaFile, string archiveFile, string sourcesDir, string confDir) {
            throw new NotImplementedException();
        }

        public static Task<IRecorder> Create(string cacheDir, string sdkDir, string schemaFile, string archiveFile, string sourcesDir, string confDir) {
            throw new NotImplementedException();
        }
    }
}