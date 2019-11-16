using System;
using System.Threading.Tasks;

namespace Helium.Engine.Record
{
    internal class NullRecorder : IRecorder
    {
        public static Task<IRecorder> Create(string cacheDir, string sdkDir, string schemaFile, string sourcesDir, string confDir) {
            throw new NotImplementedException();
        }

        public void Dispose() {
            throw new NotImplementedException();
        }
    }
}