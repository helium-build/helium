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
        Task<BuildSchema> LoadSchema() {
            throw new NotImplementedException();
        }

        IAsyncEnumerable<SdkInfo> ListAvailableSdks() {
            throw new NotImplementedException();
        }

        SdkInstallManager CreateSdkInstaller() {
            throw new NotImplementedException();
        }

        Task<RepoConfig> LoadRepoConfig() {
            throw new NotImplementedException();
        }

        string SourcesDir => throw new NotImplementedException();
    }
}