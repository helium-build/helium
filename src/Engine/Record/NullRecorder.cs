using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Helium.Engine.Build;
using Helium.Engine.Cache;
using Helium.Engine.Conf;
using Helium.Sdks;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;

namespace Helium.Engine.Record
{
    internal class NullRecorder : LiveRecorder
    {
        public NullRecorder(string cacheDir, string sdkDir, string schemaFile, string sourcesDir, string confDir) {
            this.cacheDir = cacheDir;
            SdkDir = sdkDir;
            SchemaFile = schemaFile;
            SourcesDir = sourcesDir;
            ConfDir = confDir;
        }
        
        private readonly string cacheDir;
        private readonly ConcurrentDictionary<string, Task<JObject>> metadataCache = new ConcurrentDictionary<string, Task<JObject>>();

        public override string SourcesDir { get; }
        
        protected override string SchemaFile { get; }
        protected override string SdkDir { get; }
        protected override string ConfDir { get; }


        public override Task<string> RecordArtifact(string path, Func<string, Task<string>> fetch) =>
            fetch(cacheDir);

        public override Task<JObject> RecordTransientMetadata(string path, Func<Task<JObject>> fetch) =>
            metadataCache.GetOrAdd(path, _ => fetch());

        public override ISdkInstallManager CreateSdkInstaller() => new SdkInstallManager(cacheDir);

        public override async Task RecordMetadata() { }
    }
}