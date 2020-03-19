using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Helium.Engine.Build;
using Helium.Engine.Cache;
using Helium.Engine.Conf;
using Helium.Sdks;
using Newtonsoft.Json.Linq;

namespace Helium.Engine.Record
{
    internal interface IRecorder : IAsyncDisposable
    {
        Task<string> RecordArtifact(string path, Func<string, Task<string>> fetch);

        Task<JObject> RecordTransientMetadata(string path, Func<Task<JObject>> fetch);

        Task<BuildSchema> LoadSchema();

        IAsyncEnumerable<SdkInfo> ListAvailableSdks();

        ISdkInstallManager CreateSdkInstaller();

        Task<Config> LoadRepoConfig();

        Task RecordMetadata();

        string SourcesDir { get; }
        
        string? CurrentDir { get; }
        
    }
}