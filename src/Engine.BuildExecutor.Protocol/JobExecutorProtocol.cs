using System.Collections.Generic;
using System.ComponentModel;

namespace Helium.Engine.BuildExecutor.Protocol
{
    public enum SocketState {
        Waiting,
        Running,
    }

    public abstract class Command {
        internal Command() {}
    }

    [DisplayName("stop")]
    public sealed class StopCommand : Command {
        public bool Stop { get; set; }
    }

    [DisplayName("run-docker")]
    public sealed class RunDockerCommand : Command {
        public Dictionary<string, string> Environment { get; set; } = new Dictionary<string, string>();

        public List<DockerBindMount> BindMounts { get; set; } = new List<DockerBindMount>();

        public string? ImageName { get; set; }

        public List<string> Command { get; set; } = new List<string>();
            
        public string? CurrentDirectory { get; set; }
    }

    public sealed class DockerBindMount {
        public string? HostDirectory { get; set; }
        public string? MountPath { get; set; }
        public bool IsReadOnly { get; set; }
    }

    public sealed class RunDockerExitCode {
        public int ExitCode { get; set; }
    }


    [DisplayName("run-build")]
    public sealed class RunDockerBuild {
        public string? ProxyImage { get; set; }
        public string? WorkspaceTar { get; set; }
        public Dictionary<string, string> BuildArgs { get; set; } = new Dictionary<string, string>();
        public string? OutputFile { get; set; }
        public string? CacheDirectory { get; set; }
    }
}