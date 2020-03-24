using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using Helium.Engine.Build;
using Helium.Engine.Docker;
using Helium.Engine.ContainerBuild;
using Helium.Engine.Build.Record;
using Helium.Sdks;
using static Helium.Env.Directories;

namespace Helium.Engine
{
    public static class Program
    {

        public static async Task<int> Main(string[] args) =>
            await Parser.Default.ParseArguments<BuildOptions, ReplayOptions, ContainerBuild>(args)
                .MapResult<BuildOptions, ReplayOptions, ContainerBuild, Task<int>>(
                    BuildMain,
                    ReplayMain,
                    ContainerBuildMain,
                    async errs => 1
                );

        private static async Task<int> BuildMain(BuildOptions options) {
            var workDir = options.WorkDir;
            if(workDir == null) {
                return 1;
            }

            var launcher = DetectLauncher();
            
            var outputDir = options.Output ?? Path.Combine(workDir, "output");
            var sourcesDir = options.Sources ?? Path.Combine(workDir, "sources");
            var archive = options.Archive;

            string schemaFile;
            if(options.Schema != null) {
                schemaFile = options.Schema;
            }
            else {
                var schemaFile1 = Path.Combine(workDir, "build.toml");
                if(File.Exists(schemaFile1)) {
                    schemaFile = schemaFile1;
                }
                else {
                    schemaFile = Path.Combine(sourcesDir, "build.toml");
                }
            }

            Func<Task<IRecorder>> recorder;
            if(archive != null) {
                recorder = () => ArchiveRecorder.Create(
                    cacheDir: CacheDir,
                    sdkDir: SdkDir,
                    schemaFile: schemaFile,
                    archiveFile: archive,
                    currentDir: options.CurrentDir,
                    sourcesDir: sourcesDir,
                    confDir: ConfDir
                );
            }
            else {
                recorder = () => Task.FromResult<IRecorder>(new NullRecorder(
                    cacheDir: CacheDir,
                    sdkDir: SdkDir,
                    schemaFile: schemaFile,
                    currentDir: options.CurrentDir,
                    sourcesDir: sourcesDir,
                    confDir: ConfDir
                ));
            }

            return await BuildManager.RunBuild(
                launcher,
                createRecorder: recorder,
                outputDir: outputDir,
                workDir: workDir
            );
        }
        
        private static async Task<int> ReplayMain(ReplayOptions options) {
            if(options.WorkDir == null || options.Archive == null || options.Output == null) {
                return 1;
            }
            
            var launcher = DetectLauncher();
            
            return await BuildManager.RunBuild(
                launcher,
                createRecorder: () => ReplayRecorder.Create(
                    archiveFile: options.Archive,
                    workDir: options.WorkDir
                ),
                outputDir: options.Output,
                workDir: options.WorkDir
            );
        }

        private static async Task<int> ContainerBuildMain(ContainerBuild options) {

            var launcher = DetectLauncher();
            
            var platform = new PlatformInfo(
                os: options.OperatingSystem ?? throw new Exception("Invalid OS."),
                arch: options.Architecture ?? throw new Exception("Invalid Architecture")
            );

            if(options.Workspace == null) {
                throw new Exception("Workspace is missing.");
            }

            if(options.OutputFile == null) {
                throw new Exception("Output file is missing.");
            }

            string buildContext = options.BuildContext ?? Path.Combine(options.Workspace, "build-context");
            
            var dockerfilePath = options.DockerfilePath ?? Path.Combine(buildContext, "Dockerfile");
            
            return await ContainerBuildManager.Run(launcher, options.Workspace, buildContext, dockerfilePath, options.OutputFile, platform);
        }
        
        private static ILauncher DetectLauncher() {
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
        
        
        [Verb("build", HelpText = "Runs a new build.")]
        private class BuildOptions
        {
            [Option("schema", HelpText = "The TOML file that defines the build.")]
            public string? Schema { get; set; }
        
            [Option("sources", HelpText = "The directory containing the source code for the application.")]
            public string? Sources { get; set; }
        
            [Option("output", HelpText = "The directory that will contain published artifacts.")]
            public string? Output { get; set; }
        
            [Option("archive", HelpText = "The archive file (tar) that will contain the dependencies required to reproduce the build.")]
            public string? Archive { get; set; }
            
            [Option("current-dir", HelpText = "The current directory for the build (within the build directory).")]
            public string? CurrentDir { get; set; }
        
            [Value(0, MetaName = "workDir", Required = true, HelpText = "The directory that contains the build.")]
            public string? WorkDir { get; set; }
        }
        
        [Verb("replay", HelpText = "Replays a build that was previously recorded.")]
        private class ReplayOptions
        {
            [Value(0, Required = true, MetaName = "archive", HelpText = "The archive (tar) file to replay.")]
            public string? Archive { get; set; }

            [Value(1, Required = true, MetaName = "workDir", HelpText = "The working directory.")]
            public string? WorkDir { get; set; }
        
            [Value(2, Required = true, MetaName = "output", HelpText = "The directory that will contain published artifacts.")]
            public string? Output { get; set; }
        }
        
        [Verb("container-build", HelpText = "Builds a container image.")]
        private class ContainerBuild
        {
            [Option("os", Required = true, HelpText = "The operating system inside the container.")]
            public SdkOperatingSystem? OperatingSystem { get; set; }
            
            [Option("arch", Required = true, HelpText = "The architecture of the OS inside the container.")]
            public SdkArch? Architecture { get; set; }
            
            [Option('f', "file", HelpText = "The path to the dockerfile.")]
            public string? DockerfilePath { get; set; }
            
            [Option("build-context", HelpText = "The build context.")]
            public string? BuildContext { get; set; }
            
            [Value(0, Required = true, MetaName = "workspace", HelpText = "The workspace for the docker build.")]
            public string? Workspace { get; set; }

            [Value(1, Required = true, MetaName = "outputFile", HelpText = "The docker image exported as a tar file.")]
            public string? OutputFile { get; set; }
        }

    }
}
