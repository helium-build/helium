using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Helium.Engine.Build;
using Helium.Sdks;

namespace Helium.Engine.Record
{
    internal interface IRecorder : IDisposable
    {
        Task<BuildSchema> LoadSchema() {
            throw new NotImplementedException();
        }

        IAsyncEnumerable<SdkInfo> ListAvailableSdks() {
            throw new NotImplementedException();
        }
    }
}