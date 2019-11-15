using System.Collections.Generic;
using Helium.Sdks;

namespace Helium.SdkGenerator
{
    public interface ISdkCreator
    {
        string Name { get; }
        IAsyncEnumerable<(string, SdkInfo)> GenerateSdks();
    }
}