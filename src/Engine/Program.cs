using System.Threading.Tasks;
using CommandLine;
using Helium.Engine.Build;
using Helium.Engine.ContainerBuild;
using Helium.Sdks;

namespace Helium.Engine
{
    internal static class Program
    {

        public static async Task<int> Main(string[] args) =>
            await Parser.Default.ParseArguments<BuildOptions, ReplayOptions, ContainerBuild>(args)
                .MapResult<BuildOptions, ReplayOptions, ContainerBuild, Task<int>>(
                    BuildProgram.BuildMain,
                    BuildProgram.ReplayMain,
                    ContainerBuildProgram.ContainerBuildMain,
                    async errs => 1
                );
        
        
        [Verb("build", HelpText = "Runs a new build.")]
        public class BuildOptions
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
        public class ReplayOptions
        {
            [Value(0, Required = true, MetaName = "archive", HelpText = "The archive (tar) file to replay.")]
            public string? Archive { get; set; }

            [Value(1, Required = true, MetaName = "workDir", HelpText = "The working directory.")]
            public string? WorkDir { get; set; }
        
            [Value(2, Required = true, MetaName = "output", HelpText = "The directory that will contain published artifacts.")]
            public string? Output { get; set; }
        }
        
        [Verb("container-build", HelpText = "Builds a container image.")]
        public class ContainerBuild
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
