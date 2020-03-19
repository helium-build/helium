using System.Collections.Generic;

namespace Helium.Engine.Docker
{
    internal sealed class LaunchProperties
    {
        public LaunchProperties(string dockerImage, List<string> command, string sources, string socketDir, string installDir, string? currentDirectory) {
            DockerImage = dockerImage;
            Command = command;
            Sources = sources;
            SocketDir = socketDir;
            InstallDir = installDir;
            CurrentDirectory = currentDirectory;
        }

        public string DockerImage { get; }
        public List<string> Command { get; }
        public string Sources { get; }
        
        public string SocketDir { get; }
        public string InstallDir { get; }
        
        public string? CurrentDirectory { get; }

        public Dictionary<string, string> Environment { get; } = new Dictionary<string, string>();
        public List<string> PathDirs { get; } = new List<string>();
        public List<(string containerDir, string hostDir)> SdkDirs { get; } = new List<(string containerDir, string hostDir)>();
        public List<string> ConfigFiles { get; } = new List<string>();
        
    }
}