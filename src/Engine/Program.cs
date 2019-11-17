using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using Helium.Engine.Record;

namespace Helium.Engine
{
    public static class Program
    {
        internal static string AppDir { get; } =
            Environment.GetEnvironmentVariable("HELIUM_BASE_DIR") is {} appDir
                ? appDir
                : Environment.CurrentDirectory;

        private static string ConfDir { get; } =
            Path.Combine(AppDir, "conf");

        private static string CacheDir { get; } =
            Path.Combine(AppDir, "cache");

        private static string SdkDir { get; } =
            Path.Combine(AppDir, "sdks");
        
        
        
        public static async Task<int> Main(string[] args) =>
            await Parser.Default.ParseArguments<BuildOptions, ReplayOptions>(args)
                .MapResult<BuildOptions, ReplayOptions, Task<int>>(
                    BuildMain,
                    ReplayMain,
                    async errs => 1
                );

        private static async Task<int> BuildMain(BuildOptions options) {
            var workDir = options.WorkDir;
            if(workDir == null) {
                return 1;
            }
            
            var outputDir = options.Schema ?? Path.Combine(workDir, "output");
            var sourcesDir = options.Schema ?? Path.Combine(workDir, "sources");
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
                    sourcesDir: sourcesDir,
                    confDir: ConfDir
                );
            }
            else {
                recorder = () => NullRecorder.Create(
                    cacheDir: CacheDir,
                    sdkDir: SdkDir,
                    schemaFile: schemaFile,
                    sourcesDir: sourcesDir,
                    confDir: ConfDir
                );
            }

            return await BuildManager.RunBuild(createRecorder: recorder, outputDir: outputDir, workDir: workDir);
        }
        
        private static async Task<int> ReplayMain(ReplayOptions options) {
            if(options.WorkDir == null || options.Archive == null || options.Output == null) {
                return 1;
            }
            
            return await BuildManager.RunBuild(
                createRecorder: () => ReplayRecorder.Create(
                    archiveFile: options.Archive,
                    workDir: options.WorkDir
                ),
                outputDir: options.Output,
                workDir: options.WorkDir
            );
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
        
    }
}
