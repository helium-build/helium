using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using JsonSubTypes;
using Newtonsoft.Json;

namespace Helium.Engine.BuildExecutor.Protocol
{
    public enum SocketState {
        Waiting,
        Running,
    }

    [JsonConverter(typeof(JsonSubtypes), "Action")]
    [JsonSubtypes.KnownSubType(typeof(RunDockerCommand), "RunDocker")]
    [JsonSubtypes.KnownSubType(typeof(RunDockerBuild), "RunDockerBuild")]
    public abstract class CommandBase {
        internal CommandBase() {}
        
        public abstract string Action { get; }
    }

    public sealed class StopCommand : CommandBase
    {
        public StopCommand(bool stop) {
            Stop = stop;
        }

        public override string Action => "Stop";
        
        public bool Stop { get; }
    }

    public sealed class RunDockerCommand : CommandBase {
        public RunDockerCommand(
            string imageName,
            IEnumerable<string> command,
            string? currentDirectory = null,
            IReadOnlyDictionary<string, string>? environment = null,
            IEnumerable<DockerBindMount>? bindMounts = null
        ) {
            ImageName = imageName;
            Command = new ReadOnlyCollection<string>(command.ToList());
            CurrentDirectory = currentDirectory;
            Environment = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(environment ?? new Dictionary<string, string>()));
            BindMounts = new ReadOnlyCollection<DockerBindMount>((bindMounts ?? Enumerable.Empty<DockerBindMount>()).ToList());
        }

        public override string Action => "RunDocker";

        public string ImageName { get; }

        public IReadOnlyList<string> Command { get; }
        
        public string? CurrentDirectory { get; }

        public IReadOnlyDictionary<string, string> Environment { get; }

        public IReadOnlyList<DockerBindMount> BindMounts { get; }
        
    }

    public sealed class DockerBindMount {
        public DockerBindMount(string hostDirectory, string mountPath, bool isReadOnly = false) {
            HostDirectory = hostDirectory;
            MountPath = mountPath;
            IsReadOnly = isReadOnly;
        }

        public string HostDirectory { get; }
        public string MountPath { get; }
        public bool IsReadOnly { get; }
    }

    public sealed class RunDockerExitCode {
        public int ExitCode { get; set; }
    }


    public sealed class RunDockerBuild : CommandBase {
        public RunDockerBuild(
            string proxyImage,
            string cacheDirectory,
            bool enableProxyNetwork,
            string buildContextArchive,
            string outputFile,
            IReadOnlyDictionary<string, string>? buildArgs = null
        ) {
            ProxyImage = proxyImage;
            CacheDirectory = cacheDirectory;
            EnableProxyNetwork = enableProxyNetwork;
            BuildContextArchive = buildContextArchive;
            OutputFile = outputFile;
            BuildArgs = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(buildArgs ?? new Dictionary<string, string>()));
        }

        public override string Action => "RunDockerBuild";

        public string ProxyImage { get; }
        public string CacheDirectory { get; }
        public bool EnableProxyNetwork { get; }
        public string BuildContextArchive { get; }
        public IReadOnlyDictionary<string, string> BuildArgs { get; }
        public string OutputFile { get; }
    }
}