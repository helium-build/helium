using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Helium.Engine.Docker;
using Helium.Sdks;
using Newtonsoft.Json;
using Helium.Engine.BuildExecutor.Protocol;

namespace Helium.Engine.Docker
{
    internal class BuildExecutorWebSocketLauncher : LauncherBase
    {
        public override Task<int> Run(PlatformInfo platform, LaunchProperties props) {
            var runCommand = BuildRunCommand(platform, props);
            return RunWebSocketExecutor(runCommand, CancellationToken.None);
        }
            

        private async Task<int> RunWebSocketExecutor(CommandBase command, CancellationToken cancellationToken) {
            var uri = Environment.GetEnvironmentVariable("HELIUM_JOB_EXECUTOR_URL");
            if(uri == null) {
                throw new Exception("Variable HELIUM_JOB_EXECUTOR_URL not specified.");
            }

            var ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri(uri), cancellationToken);

            var startMessage = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(command));
            await ws.SendAsync(new ArraySegment<byte>(startMessage), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);

            await using var stdout = Console.OpenStandardOutput();
            await using var stderr = Console.OpenStandardError();
            var buffer = new byte[4096];

            async Task HandleOutput(Stream stream) {
                while(!cancellationToken.IsCancellationRequested) {
                    var msg = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    if(msg.MessageType == WebSocketMessageType.Close) break;
                    if(msg.MessageType == WebSocketMessageType.Text) continue;
                    await stream.WriteAsync(buffer, 0, msg.Count, cancellationToken);
                }
            }

            while(!cancellationToken.IsCancellationRequested) {
                var msg = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if(msg.MessageType == WebSocketMessageType.Binary) {
                    if(msg.Count > 0) {
                        switch(buffer[0]) {
                            case 0x00:
                                await stdout.WriteAsync(buffer, 1, msg.Count - 1, cancellationToken);
                                if(!msg.EndOfMessage) await HandleOutput(stdout);
                                break;

                            case 0x01:
                                await stderr.WriteAsync(buffer, 1, msg.Count - 1, cancellationToken);
                                if(!msg.EndOfMessage) await HandleOutput(stderr);
                                break;


                        }
                    }
                }
                else {
                    string json;
                    if(msg.EndOfMessage) {
                        json = Encoding.UTF8.GetString(buffer, 0, msg.Count);
                    }
                    else {
                        var memoryStream = new MemoryStream();

                        memoryStream.Write(buffer, 0, msg.Count);

                        while(!cancellationToken.IsCancellationRequested && !msg.EndOfMessage) {
                            msg = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                            if(msg.MessageType == WebSocketMessageType.Close) break;
                            if(msg.MessageType == WebSocketMessageType.Binary) continue;
                            memoryStream.Write(buffer, 0, msg.Count);
                        }

                        json = Encoding.UTF8.GetString(memoryStream.ToArray());
                    }

                    var exitCode = JsonConvert.DeserializeObject<RunDockerExitCode>(json);
                    
                    return exitCode.ExitCode;
                }
            }

            throw new OperationCanceledException();
        }

        public override Task<int> BuildContainer(PlatformInfo platform, ContainerBuildProperties props) {
            var containerBuildCommand = BuildContainerBuildCommand(platform, props);
            return RunWebSocketExecutor(containerBuildCommand, CancellationToken.None);
        }
    }
}