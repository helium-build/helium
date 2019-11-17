using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Helium.Engine.Build;
using Helium.Engine.Cache;
using Helium.Engine.Conf;
using Helium.Sdks;

namespace Helium.Engine.Record
{
    public abstract class LiveRecorder : IRecorder
    {

        protected abstract string SchemaFile { get; }
        protected abstract string SdkDir { get; }
        protected abstract string ConfDir { get; }

        public abstract string SourcesDir { get; }

        public abstract SdkInstallManager CreateSdkInstaller();

        
        public async ValueTask DisposeAsync() { }
        
        protected virtual Task<string> CacheBuildSchema(Func<Task<string>> readBuildSchema) => readBuildSchema();
        protected virtual Task<string> CacheRepoConfig(Func<Task<string>> readRepoConfig) => readRepoConfig();

        public async Task<BuildSchema> LoadSchema() => BuildSchema.Parse(
            await CacheBuildSchema(() => File.ReadAllTextAsync(SchemaFile, Encoding.UTF8))
        );

        public async IAsyncEnumerable<SdkInfo> ListAvailableSdks() {
            foreach(var sdkTask in SdkLoader.loadSdks.Invoke(SdkDir)) {
                yield return await sdkTask;
            }
        }

        public async Task<RepoConfig> LoadRepoConfig() => RepoConfig.Parse(
            await CacheRepoConfig(() => File.ReadAllTextAsync(Path.Combine(ConfDir, "repos.toml"), Encoding.UTF8))
        );
    }
}