using JsonSubTypes;
using Newtonsoft.Json;

namespace Helium.Sdks
{
    [JsonConverter(typeof(JsonSubtypes), "Action")]
    [JsonSubtypes.KnownSubTypeAttribute(typeof(Download), nameof(Download))]
    [JsonSubtypes.KnownSubTypeAttribute(typeof(Extract), nameof(Extract))]
    [JsonSubtypes.KnownSubTypeAttribute(typeof(Delete), nameof(Delete))]
    [JsonSubtypes.KnownSubTypeAttribute(typeof(CreateDirectory), nameof(CreateDirectory))]
    [JsonSubtypes.KnownSubTypeAttribute(typeof(CreateFile), nameof(CreateFile))]
    public abstract class SdkSetupStep
    {
        private SdkSetupStep() {}
        
        public abstract string Action { get; }


        public sealed class Download : SdkSetupStep
        {
            public Download(string url, string fileName, SdkHash hash) {
                Url = url;
                FileName = fileName;
                Hash = hash;
            }
            
            public override string Action => nameof(Download);
            public string Url { get; }
            public string FileName { get; }
            public SdkHash Hash { get; }
        }

        public sealed class Extract : SdkSetupStep
        {
            public Extract(string fileName, string directory) {
                FileName = fileName;
                Directory = directory;
            }
            
            public override string Action => nameof(Extract);
            public string FileName { get; }
            public string Directory { get; }
        }

        public sealed class Delete : SdkSetupStep
        {
            public Delete(string fileName) {
                FileName = fileName;
            }
            
            public override string Action => nameof(Delete);
            public string FileName { get; }
        }

        public sealed class CreateDirectory : SdkSetupStep
        {
            public CreateDirectory(string fileName) {
                FileName = fileName;
            }
            
            public override string Action => nameof(CreateDirectory);
            public string FileName { get; }
        }

        public sealed class CreateFile : SdkSetupStep
        {
            public CreateFile(string fileName, bool isExecutable, string content) {
                FileName = fileName;
                IsExecutable = isExecutable;
                Content = content;
            }
            
            public override string Action => nameof(CreateFile);
            public string FileName { get; }
            public bool IsExecutable { get; }
            public string Content { get; }
        }
        
        
        
        
        
    }
}