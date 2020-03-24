using System;
using System.IO;
using System.Threading.Tasks;
using Helium.Engine.Build.Record;
using Helium.Engine.Docker;
using static Helium.Env.Directories;

namespace Helium.Engine.Build
{
    internal static class BuildProgram
    {
        public static async Task<int> BuildMain(Program.BuildOptions options) {
            var workDir = options.WorkDir;
            if(workDir == null) {
                return 1;
            }

            var launcher = Launcher.DetectLauncher();
            
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
        
        public static async Task<int> ReplayMain(Program.ReplayOptions options) {
            if(options.WorkDir == null || options.Archive == null || options.Output == null) {
                return 1;
            }
            
            var launcher = Launcher.DetectLauncher();
            
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
    }
}