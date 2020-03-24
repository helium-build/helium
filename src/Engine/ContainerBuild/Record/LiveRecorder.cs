using System.IO;
using System.Threading.Tasks;
using Helium.Engine.Docker;
using Helium.Sdks;
using Helium.Util;

namespace Helium.Engine.ContainerBuild
{
    public abstract class LiveRecorder : IRecorder
    {
        public LiveRecorder(PlatformInfo platform, string workspace, string buildContext, string dockerfilePath, string imageFile) {
            Platform = platform;
            WorkspaceDir = workspace;
            this.buildContext = buildContext;
            this.dockerfilePath = dockerfilePath;
            ImageFile = imageFile;
        }

        private readonly string buildContext;
        private readonly string dockerfilePath;
        
        public PlatformInfo Platform { get; }

        public string WorkspaceDir { get; }

        public async Task<string> GetCacheDir() =>
            DirectoryUtil.CreateTempDirectory(WorkspaceDir);

        public bool EnableNetwork => true;
        
        public async Task<string> GetBuildContext() {
            string newDockerfile;
            using(var reader = File.OpenText(dockerfilePath)) {
                (newDockerfile, _) = await DockerfileResolver.ProcessDockerfile(reader, Platform);
            }

            await using var buildContextStream = FileUtil.CreateTempFile(WorkspaceDir, out var buildContextFile);
            await BuildContextHandler.WriteBuildContext(buildContext, newDockerfile, buildContextStream);
            return buildContextFile;
        }

        public string ImageFile { get; }

        public abstract Task CompleteBuild();

    }
}