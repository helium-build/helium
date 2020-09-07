using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Helium.Engine.Build;
using Helium.Engine.Build.Cache;
using Helium.Engine.Conf;
using Helium.Sdks;
using Helium.Util;
using ICSharpCode.SharpZipLib.Tar;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Helium.Engine.Build.Record
{
    internal class ReplayRecorder : IRecorder
    {
        private ReplayRecorder(string extractedDir, JObject dependencyMetadata) {
            this.extractedDir = extractedDir;
            this.dependencyMetadata = dependencyMetadata;
            SourcesDir = Path.Combine(extractedDir, ArchiveRecorder.SourcesPath);
        }

        private readonly string extractedDir;
        private readonly JObject dependencyMetadata;

        public static async Task<IRecorder> Create(string archiveFile, string workDir) {
            var extractedDirCleanup = DirectoryCleanup.CreateTempDir(workDir, async extractedDir => {
                await using(var stream = File.OpenRead(archiveFile)) {
                    await using var tarStream = new TarInputStream(stream);
                    await ArchiveUtil.ExtractTar(tarStream, extractedDir);
                }

                var dependencyMetadata = JsonConvert.DeserializeObject<JObject>(
                    await File.ReadAllTextAsync(Path.Combine(extractedDir, ArchiveRecorder.TransientMetadataPath))
                );
                
                return new ReplayRecorder(extractedDir, dependencyMetadata);
            });

            return await extractedDirCleanup.Value();
        }

        public async ValueTask DisposeAsync() {
            Directory.Delete(extractedDir, recursive: true);
        }

        public async Task<string> RecordArtifact(string path, Func<string, Task<string>> fetch) =>
            Path.Combine(extractedDir, ArchiveRecorder.ArtifactPath(path));

        public async Task<JObject> RecordTransientMetadata(string path, Func<Task<JObject>> fetch) =>
            (JObject?)dependencyMetadata[path] ?? throw new Exception("Could not find metadata path.");

        public async Task<BuildSchema> LoadSchema() {
            var text = await File.ReadAllTextAsync(Path.Combine(extractedDir, ArchiveRecorder.BuildSchemaPath));
            return BuildSchema.Parse(text);
        }

        public async IAsyncEnumerable<SdkInfo> ListAvailableSdks() {
            foreach(var subDir in Directory.GetDirectories(Path.Combine(extractedDir, "sdks"))) {
                yield return await SdkLoader.LoadSdk(Path.Combine(subDir, "sdk.json"));
            }
        }

        public ISdkInstallManager CreateSdkInstaller() =>
            new ReplaySdkManager(this);

        public async Task<Config> LoadRepoConfig() {
            var confData = await File.ReadAllTextAsync(Path.Combine(extractedDir, ArchiveRecorder.RepoConfigPath));
            return new Config {
                repos = Repos.Parse(confData),
            };
        }

        public async Task RecordMetadata() { }

        public string SourcesDir { get; }
        public string? CurrentDir => null;

        private sealed class ReplaySdkManager : ISdkInstallManager
        {
            public ReplaySdkManager(ReplayRecorder replayRecorder) {
                this.replayRecorder = replayRecorder;
            }

            private readonly ReplayRecorder replayRecorder;

            public async Task<(string hash, string installDir)> GetInstalledSdkDir(SdkInfo sdk) {
                var sdkHash = SdkLoader.SdkSha256(sdk);
                var sdkDir = Path.Combine(replayRecorder.extractedDir, ArchiveRecorder.SdkPath(sdkHash));

                if(!Directory.Exists(sdkDir)) {
                    throw new Exception("Unknown SDK.");
                }

                return (sdkHash, Path.Combine(sdkDir, "install"));
            }
        }
        
    }
}