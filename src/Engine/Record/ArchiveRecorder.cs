using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Helium.Engine.Cache;
using Helium.Sdks;
using Helium.Util;
using ICSharpCode.SharpZipLib.Tar;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;

namespace Helium.Engine.Record
{
    internal class ArchiveRecorder : LiveRecorder
    {
        private ArchiveRecorder(string cacheDir, string sdkDir, string schemaFile, string sourcesDir, string confDir, Stream tarDataStream, TarOutputStream tarStream) {
            this.cacheDir = cacheDir;
            this.tarDataStream = tarDataStream;
            this.tarStream = tarStream;
            SdkDir = sdkDir;
            SchemaFile = schemaFile;
            SourcesDir = sourcesDir;
            ConfDir = confDir;
        }

        private readonly string cacheDir;
        private readonly Stream tarDataStream;
        private readonly ConcurrentDictionary<string, Task<JObject>> metadataCache = new ConcurrentDictionary<string, Task<JObject>>();
        
        private readonly AsyncLock tarLock = new AsyncLock();
        private readonly TarOutputStream tarStream;
        private readonly HashSet<string> recordedSdks = new HashSet<string>();
        private readonly HashSet<string> recordedArtifacts = new HashSet<string>();
        private bool hasRecordedSchema;
        private bool hasRecordedRepoConfig;

        public override string SourcesDir { get; }
        protected override string SchemaFile { get; }
        protected override string SdkDir { get; }
        protected override string ConfDir { get; }


        public override async ValueTask DisposeAsync() {
            try {
                await tarStream.DisposeAsync();
            }
            finally {
                await tarDataStream.DisposeAsync();
            }
        }

        public override ISdkInstallManager CreateSdkInstaller() =>
            new ArchiveSdkInstallManager(this);

        public override async Task<string> RecordArtifact(string path, Func<string, Task<string>> fetch) {
            var file = await fetch(cacheDir);

            using var _ = await tarLock.LockAsync();

            if(!recordedArtifacts.Contains(path)) {
                await ArchiveUtil.AddFileToTar(tarStream, ArtifactPath(path), file);
                recordedArtifacts.Add(path);
            }
            
            return file;
        }


        public override Task<JObject> RecordTransientMetadata(string path, Func<Task<JObject>> fetch) =>
            metadataCache.GetOrAdd(path, _ => fetch());


        protected override async Task<string> RecordBuildSchema(Func<Task<string>> readBuildSchema) {
            var result = await readBuildSchema();

            using var _ = await tarLock.LockAsync();
            if(!hasRecordedSchema) {
                await ArchiveUtil.AddFileToTar(tarStream, BuildSchemaPath, SchemaFile);
                hasRecordedSchema = true;
            }
            
            return result;
        }

        protected override async Task<string> RecordRepoConfig(Func<Task<string>> readRepoConfig) {
            var result = await readRepoConfig();

            using var _ = await tarLock.LockAsync();
            if(!hasRecordedRepoConfig) {
                await ArchiveUtil.AddFileToTar(tarStream, RepoConfigPath, Path.Combine(ConfDir, "repos.toml"));
                hasRecordedRepoConfig = true;
            }
            
            return result;
        }

        public override async Task RecordMetadata() {
            var obj = new JObject();
            foreach(var (key, value) in metadataCache) {
                obj[key] = await value;
            }

            await ArchiveUtil.AddStringToTar(tarStream, TransientMetadataPath, JsonConvert.SerializeObject(obj));
        }

        private sealed class ArchiveSdkInstallManager : SdkInstallManager
        {
            public ArchiveSdkInstallManager(ArchiveRecorder archiveRecorder) : base(archiveRecorder.cacheDir) {
                this.archiveRecorder = archiveRecorder;
            }

            private readonly ArchiveRecorder archiveRecorder;
            
            public override async Task<(string hash, string installDir)> GetInstalledSdkDir(SdkInfo sdk) {
                var (hash, installDir) = await base.GetInstalledSdkDir(sdk);

                using var _ = await archiveRecorder.tarLock.LockAsync();
                
                if(!archiveRecorder.recordedSdks.Contains(hash)) {
                    await ArchiveUtil.AddDirToTar(archiveRecorder.tarStream, SdkPath(hash), Path.GetFullPath(Path.Combine(installDir, "..")));
                    archiveRecorder.recordedSdks.Add(hash);
                }
                
                return (hash, installDir);
            }
        }

        public static async Task<IRecorder> Create(string cacheDir, string sdkDir, string schemaFile, string sourcesDir, string confDir, string archiveFile) {
            var stream = File.Create(archiveFile);
            TarOutputStream tarStream;
            try {
                tarStream = new TarOutputStream(stream);
                try {
                    await ArchiveUtil.AddDirToTar(tarStream, SourcesPath, sourcesDir);
                }
                catch {
                    try {
                        await tarStream.DisposeAsync();
                    }
                    catch {}
                    throw;
                }
            }
            catch {
                try {
                    await stream.DisposeAsync();
                }
                catch {}
                throw;
            }
            
            return new ArchiveRecorder(
                cacheDir: cacheDir,
                sdkDir: sdkDir,
                schemaFile: schemaFile,
                sourcesDir: sourcesDir,
                confDir: confDir,
                tarDataStream: stream,
                tarStream: tarStream
            );
        }

        public const string BuildSchemaPath = "build.toml";
        public const string RepoConfigPath = "conf/repos.toml";
        public const string TransientMetadataPath = "dependencies-metadata.json";
        public const string SourcesPath = "sources";
        public static string SdkPath(string hash) => "sdks/" + hash;
        public static string ArtifactPath(string path) => "dependencies/" + path;

    }
}