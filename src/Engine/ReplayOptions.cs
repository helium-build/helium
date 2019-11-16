using CommandLine;

namespace Helium.Engine
{
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
}