using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Helium.Engine.BuildExecutor;

internal static class DockerHelpers
{
    public static async Task PipeContainerOutput(IOutputObserver outputObserver, MultiplexedStream stream, CancellationToken cancellationToken) {
        byte[] buffer = new byte[1024];
        while(!cancellationToken.IsCancellationRequested) {
            var result = await stream.ReadOutputAsync(buffer, 0, buffer.Length, cancellationToken);

            if(result.EOF) {
                break;
            }

            switch(result.Target) {
                case MultiplexedStream.TargetStream.StandardOut:
                    await outputObserver.StandardOutput(buffer, result.Count);
                    break;

                case MultiplexedStream.TargetStream.StandardError:
                    await outputObserver.StandardError(buffer, result.Count);
                    break;
            }
        }
    }
    
}