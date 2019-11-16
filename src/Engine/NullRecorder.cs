using System;
using System.Threading.Tasks;
using Helium.Engine.Record;

namespace Helium.Engine
{
    internal class NullRecorder
    {
        public static Task<IRecorder> Create(string cacheDir, string sdkDir, string schemaFile, string sourcesDir, string confDir) {
            throw new NotImplementedException();
        }
    }
}