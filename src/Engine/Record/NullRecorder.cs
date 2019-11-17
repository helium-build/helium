using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Helium.Engine.Build;
using Helium.Engine.Cache;
using Helium.Engine.Conf;
using Helium.Sdks;

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

        public override string SourcesDir { get; }
        
        protected override string SchemaFile { get; }
        protected override string SdkDir { get; }
        protected override string ConfDir { get; }
        
        public override SdkInstallManager CreateSdkInstaller() => new SdkInstallManager(cacheDir);
            
    }
}