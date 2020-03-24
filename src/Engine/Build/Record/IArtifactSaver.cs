using System;
using System.IO;
using System.Threading.Tasks;

namespace Helium.Engine.Build.Record
{
    public interface IArtifactSaver
    {
        Task SaveArtifact(string name, Stream stream);
        Task SaveArtifact(Stream stream, Func<string, Task<string>> nameSelector);
    }
}