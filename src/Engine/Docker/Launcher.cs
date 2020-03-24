using System;

namespace Helium.Engine.Docker
{
    internal static class Launcher
    {
        public static ILauncher DetectLauncher() {
            var sudoCommand = Environment.GetEnvironmentVariable("HELIUM_SUDO_COMMAND");
            var dockerCommand = Environment.GetEnvironmentVariable("HELIUM_DOCKER_COMMAND") ?? "docker";
            
            switch(Environment.GetEnvironmentVariable("HELIUM_LAUNCH_MODE")) {
                case "docker-cli":
                case null:
                    return new DockerCLILauncher(sudoCommand, dockerCommand);
                
                case "build-executor-cli":
                    return new BuildExecutorCLILauncher(sudoCommand, dockerCommand);
                
                case "build-executor-websocket":
                    return new BuildExecutorWebSocketLauncher();
                
                default:
                    throw new Exception("Unknown value for HELIUM_LAUNCH_MODE");
            }
        }
    }
}