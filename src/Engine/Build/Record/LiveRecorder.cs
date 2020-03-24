using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Helium.Engine.Build;
using Helium.Engine.Build.Cache;
using Helium.Engine.Conf;
using Helium.Sdks;
using Helium.Util;
using Newtonsoft.Json.Linq;

namespace Helium.Engine.Build.Record
{
    public abstract class LiveRecorder : IRecorder
    {

        protected abstract string SchemaFile { get; }
        protected abstract string SdkDir { get; }
        protected abstract string ConfDir { get; }

        public abstract string SourcesDir { get; }
        public abstract string? CurrentDir { get; }

        public abstract ISdkInstallManager CreateSdkInstaller();

        
        public virtual async ValueTask DisposeAsync() { }

        public abstract Task<string> RecordArtifact(string path, Func<string, Task<string>> fetch);

        public abstract Task<JObject> RecordTransientMetadata(string path, Func<Task<JObject>> fetch);

        protected virtual Task<string> RecordBuildSchema(Func<Task<string>> readBuildSchema) => readBuildSchema();
        protected virtual Task<string> RecordRepoConfig(Func<Task<string>> readRepoConfig) => readRepoConfig();

        public async Task<BuildSchema> LoadSchema() => BuildSchema.Parse(
            await RecordBuildSchema(() => File.ReadAllTextAsync(SchemaFile, Globals.HeliumEncoding))
        );

        public async IAsyncEnumerable<SdkInfo> ListAvailableSdks() {
            foreach(var sdkTask in SdkLoader.loadSdks.Invoke(SdkDir)) {
                yield return await sdkTask;
            }
        }

        public async Task<Config> LoadRepoConfig() => new Config {
            repos = Repos.Parse(
                await RecordRepoConfig(() => File.ReadAllTextAsync(Path.Combine(ConfDir, "repos.toml"), Globals.HeliumEncoding))
            ),
        };

        public abstract Task RecordMetadata();
    }
}