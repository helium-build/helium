using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Helium.DockerfileHandler;
using Helium.Engine.Docker;
using Helium.Sdks;
using Helium.Util;

namespace Helium.Engine.ContainerBuild
{
    public abstract class LiveRecorder : IRecorder
    {
        public LiveRecorder(PlatformInfo platform, string workspace, string buildContext, string dockerfilePath, string imageFile, IReadOnlyDictionary<string, string> buildArgs) {
            Platform = platform;
            WorkspaceDir = workspace;
            BuildContext = buildContext;
            this.dockerfilePath = dockerfilePath;
            ImageFile = imageFile;
            BuildArgs = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(buildArgs));
        }

        private readonly string dockerfilePath;
        
        public PlatformInfo Platform { get; }
        
        public IReadOnlyDictionary<string, string> BuildArgs { get; }

        public string WorkspaceDir { get; }

        public async Task<string> GetCacheDir() =>
            DirectoryUtil.CreateTempDirectory(WorkspaceDir);

        public bool EnableNetwork => true;

        public string ImageFile { get; }
        public string BuildContext { get; }

        public abstract Task CompleteBuild();

        public async Task<DockerfileInfo> LoadDockerfile() {
            using var reader = File.OpenText(dockerfilePath);
            return await DockerfileResolver.ProcessDockerfile(reader, Platform, BuildArgs);
        }
    }
}