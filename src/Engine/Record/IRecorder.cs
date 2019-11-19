using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Helium.Engine.Build;
using Helium.Engine.Cache;
using Helium.Engine.Conf;
using Helium.Sdks;

namespace Helium.Engine.Record
{
    internal interface IRecorder : IAsyncDisposable
    {
        Task<string> RecordArtifact(string path, Func<string, Task<string>> fetch) {
            throw new NotImplementedException();
        }
        
        Task<BuildSchema> LoadSchema() {
            throw new NotImplementedException();
        }

        IAsyncEnumerable<SdkInfo> ListAvailableSdks() {
            throw new NotImplementedException();
        }

        SdkInstallManager CreateSdkInstaller() {
            throw new NotImplementedException();
        }

        Task<Config> LoadRepoConfig() {
            throw new NotImplementedException();
        }

        string SourcesDir => throw new NotImplementedException();
    }
}