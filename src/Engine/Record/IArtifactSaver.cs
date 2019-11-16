using System;
using System.IO;
using System.Threading.Tasks;

namespace Helium.Engine.Record
{
    public interface IArtifactSaver
    {
        Task SaveArtifact(string name, Stream stream);
        Task SaveArtifact(string name, Func<string, Task<string>> nameSelector);
    }
}