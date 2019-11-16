using CommandLine;

namespace Helium.Engine
{
    [Verb("build", HelpText = "Runs a new build.")]
    internal class BuildOptions
    {
        [Option("schema", HelpText = "The TOML file that defines the build.")]
        public string? Schema { get; set; }
        
        [Option("sources", HelpText = "The directory containing the source code for the application.")]
        public string? Sources { get; set; }
        
        [Option("output", HelpText = "The directory that will contain published artifacts.")]
        public string? Output { get; set; }
        
        [Option("archive", HelpText = "The archive file (tar) that will contain the dependencies required to reproduce the build.")]
        public string? Archive { get; set; }
        
        [Value(0, MetaName = "workDir", Required = true, HelpText = "The directory that contains the build.")]
        public string? WorkDir { get; set; }
    }
}