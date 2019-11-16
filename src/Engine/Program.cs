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
        private static string AppDir { get; } =
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
            if(options.Archive != null) {
                recorder = () => ArchiveRecorder.Create(
                    cacheDir: CacheDir,
                    sdkDir: SdkDir,
                    schemaFile: schemaFile,
                    archiveFile: options.Archive,
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

            await BuildManager.RunBuild(createRecorder: recorder, outputDir: outputDir, workDir: workDir);
            return 0;
        }
        
        private static async Task<int> ReplayMain(ReplayOptions options) {
            if(options.WorkDir == null || options.Archive == null || options.Output == null) {
                return 1;
            }
            
            await BuildManager.RunBuild(
                createRecorder: () => ReplayRecorder.Create(
                    archiveFile: options.Archive,
                    workDir: options.WorkDir
                ),
                outputDir: options.Output,
                workDir: options.WorkDir
            );
            return 0;
        }
    }
}
