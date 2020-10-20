using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Helium.CI.Common;
using Helium.Pipeline;
using Helium.Util;
using ICSharpCode.SharpZipLib.Tar;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace Helium.CI.Agent
{
    internal class BuildJobRunner
    {
        public BuildJobRunner(ILogger logger, string buildDir, IAsyncStreamReader<RunBuildCommand> commandStream, IAsyncStreamWriter<BuildStatusUpdate> updateStream, CancellationToken cancellationToken) {
            this.logger = logger;
            this.buildDir = buildDir;
            this.commandStream = commandStream;
            this.updateStream = updateStream;
            this.cancellationToken = cancellationToken;
        }

        private readonly ILogger logger;
        private readonly string buildDir;
        private readonly IAsyncStreamReader<RunBuildCommand> commandStream;
        private readonly IAsyncStreamWriter<BuildStatusUpdate> updateStream;
        private readonly CancellationToken cancellationToken;

        public async Task RunJob(BuildTaskBase? buildTaskBase) {
            await SetupWorkspace();
            
            logger.LogTrace("Copied workspace");

            Directory.CreateDirectory(ArtifactDir);
                
            var psi = buildTaskBase switch {
                BuildTask buildTask => CreateBuildProcess(buildDir, buildTask),
                ContainerBuildTask containerBuildTask => CreateContainerBuildProcess(buildDir, containerBuildTask),
                _ => throw new Exception("Invalid build task")
            };

            cancellationToken.ThrowIfCancellationRequested();
            
            var process = Process.Start(psi);
            logger.LogTrace("Running external process");
            
            var exitTask = process.WaitForExitAsync();


            var writerLock = new AsyncLock();
        
            async Task PipeData(Stream stream) {
                byte[] buffer = new byte[4096];
                int bytesRead;
                while((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0) {
                    cancellationToken.ThrowIfCancellationRequested();
                    using(await writerLock.LockAsync()) {
                        cancellationToken.ThrowIfCancellationRequested();
                        await updateStream.WriteAsync(new BuildStatusUpdate {
                            BuildOutput = ByteString.CopyFrom(buffer.AsSpan(0, bytesRead)),
                        });
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                }
            
                stream.Close();
            }
        
            var outputTask = Task.Run(() => PipeData(process.StandardOutput.BaseStream), cancellationToken);
            var errorTask = Task.Run(() => PipeData(process.StandardError.BaseStream), cancellationToken);

            await Task.WhenAll(outputTask, errorTask, exitTask);

            var artifactDir = ArtifactDir;
            var artifacts = Directory.EnumerateFiles(artifactDir, "*.json", SearchOption.AllDirectories)
                .Select(file => new ArtifactInfo {
                    Name = file.Substring(artifactDir.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                });

            var jobResult = new JobResult {
                ExitCode = process.ExitCode,
            };
            
            jobResult.Artifacts.AddRange(artifacts);
            
            await updateStream.WriteAsync(new BuildStatusUpdate {
                JobFinished = jobResult,
            });

            logger.LogTrace("Finished job");
        }
        
        private async Task SetupWorkspace() {
            var pipe = new Pipe();

            var writerTask = Task.Run(() => WriteWorkspace(pipe.Reader.AsStream()), cancellationToken);

            await ReadWorkspace(pipe.Writer.AsStream());
            await writerTask;
        }
        
        

        private async Task ReadWorkspace(Stream output) {
            try {
                cancellationToken.ThrowIfCancellationRequested();

                while(await commandStream.MoveNext()) {
                    cancellationToken.ThrowIfCancellationRequested();

                    var command = commandStream.Current;
                    switch(command.PayloadCase) {
                        case RunBuildCommand.PayloadOneofCase.WorkspaceContent:
                            await output.WriteAsync(command.WorkspaceContent.ToByteArray(), cancellationToken);
                            break;

                        case RunBuildCommand.PayloadOneofCase.WorkspaceEnd:
                            return;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            finally {
                output.Close();
            }
        }
        
        private async Task WriteWorkspace(Stream workspaceStream) {
            try {
                Directory.CreateDirectory(WorkspaceDir);
                await using var tarStream = new TarInputStream(workspaceStream);
                await ArchiveUtil.ExtractTar(tarStream, WorkspaceDir);
            }
            finally {
                workspaceStream.Close();
            }
        }


        private string WorkspaceDir => Path.Combine(buildDir, "workspace");
        private string ArtifactDir => Path.Combine(buildDir, "artifacts");
        private string ReplayFile => Path.Combine(buildDir, "replay.tar");

        private ProcessStartInfo CreateBuildProcess(string buildDir, BuildTask buildTask) {
            if(!PathUtil.IsValidSubPath(buildTask.BuildFile)) {
                throw new Exception("Invalid build file path.");
            }

            var workspaceDir = WorkspaceDir;

            var psi = new ProcessStartInfo {
                FileName = "helium",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                ArgumentList = {
                    "build",

                    "--schema",
                    Path.Combine(workspaceDir, buildTask.BuildFile),

                    "--output",
                    ArtifactDir,

                    "--sources",
                    workspaceDir,
                    
                    "--current-dir",
                    Path.GetDirectoryName(buildTask.BuildFile), // path is within the container
                },
            };

            switch(buildTask.ReplayMode) {
                case ReplayMode.Discard:
                    break;
                    
                case ReplayMode.RecordCache:
                    psi.ArgumentList.Add("--archive");
                    psi.ArgumentList.Add(ReplayFile);
                    break;
                
                default:
                    throw new Exception("Invalid build task");
            }
            
            psi.ArgumentList.Add(buildDir);

            return psi;
        }

        private ProcessStartInfo CreateContainerBuildProcess(string buildDir, ContainerBuildTask containerBuildTask) {
            if(!PathUtil.IsValidSubPath(containerBuildTask.Dockerfile)) {
                throw new Exception("Invalid Dockerfile path.");
            }

            if(!PathUtil.IsValidSubPath(containerBuildTask.ImageFileName) ||
               containerBuildTask.ImageFileName.Contains(Path.DirectorySeparatorChar) ||
               containerBuildTask.ImageFileName.Contains(Path.AltDirectorySeparatorChar)) {
                throw new Exception("Invalid image file name.");
            }
            
            var workspaceDir = WorkspaceDir;
            
            var psi = new ProcessStartInfo {
                FileName = "helium",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                ArgumentList = {
                    "container-build",
                    
                    "--os",
                    containerBuildTask.Platform.OS.ToString(),
                    
                    "--arch",
                    containerBuildTask.Platform.Arch.ToString(),

                    "--file",
                    Path.Combine(workspaceDir, containerBuildTask.Dockerfile),

                    "--build-context",
                    workspaceDir,
                },
            };

            switch(containerBuildTask.ReplayMode) {
                case ReplayMode.Discard:
                    break;
                    
                case ReplayMode.RecordCache:
                    psi.ArgumentList.Add("--archive");
                    psi.ArgumentList.Add(ReplayFile);
                    break;
                
                default:
                    throw new Exception("Invalid build task");
            }
            
            psi.ArgumentList.Add(buildDir);
            psi.ArgumentList.Add(containerBuildTask.ImageFileName);

            return psi;
        }

        public async Task SendReplay() {
            await using var stream = File.OpenRead(ReplayFile);
            await SendStreamContent(stream);
        }

        public async Task SendArtifact(ArtifactInfo artifact) {
            if(!PathUtil.IsValidSubPath(artifact.Name)) {
                await updateStream.WriteAsync(new BuildStatusUpdate {
                    ArtifactEnd = new ArtifactEnd {
                        HasError = true,
                    },
                });
                return;
            }

            await using var stream = File.OpenRead(Path.Combine(buildDir, artifact.Name));
            await SendStreamContent(stream);
        }

        private async Task SendStreamContent(Stream stream) {
            byte[] buffer = new byte[4096];

            int bytesRead;
            while((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0) {
                cancellationToken.ThrowIfCancellationRequested();
                await updateStream.WriteAsync(new BuildStatusUpdate {
                    ArtifactData = ByteString.CopyFrom(buffer.AsSpan(0, bytesRead)),
                });
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }
}