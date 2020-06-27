using System.Collections.Generic;
using System.Threading.Tasks;
using Helium.Sdks;

namespace Helium.Engine.ContainerBuild
{
    public sealed class NullRecorder : LiveRecorder
    {
        public NullRecorder(PlatformInfo platform, string workspace, string buildContext, string dockerfilePath, string imageFile, IReadOnlyDictionary<string, string> buildArgs)
            : base(platform, workspace, buildContext, dockerfilePath, imageFile, buildArgs)
        {}

        public override async Task CompleteBuild() {}
    }
}