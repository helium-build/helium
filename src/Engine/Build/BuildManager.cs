using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using DotLiquid;
using Helium.Engine.Docker;
using Helium.Engine.Build;
using Helium.Engine.Build.Cache;
using Helium.Engine.Conf;
using Helium.Engine.Build.Proxy;
using Helium.Engine.Build.Record;
using Helium.Sdks;
using Helium.Util;

namespace Helium.Engine.Build
{
    internal static class BuildManager
    {
        public static async Task<int> RunBuild(ILauncher launcher, Func<Task<IRecorder>> createRecorder, string outputDir, string workDir) {
            await using var recorder = await createRecorder();

            Directory.CreateDirectory(outputDir);
            var artifact = new FSArtifactSaver(outputDir);

            var schema = await recorder.LoadSchema();

            var sdks = await recorder.ListAvailableSdks().ToListAsync();
            var sdkInstallManager = recorder.CreateSdkInstaller();

            var conf = await recorder.LoadRepoConfig();
            
            var currentPlatform = PlatformInfo.Current;
            
            await using var launchPropsCleanup = GetDockerLaunchProps(
                platform: currentPlatform,
                sdks: sdks,
                workDir: workDir,
                currentDir: recorder.CurrentDir,
                sourcesDir: recorder.SourcesDir,
                conf,
                sdkInstallManager,
                schema
            );

            var launchProps = await launchPropsCleanup.Value();

            await using var proxyServer = await ProxyServer.Create(
                Path.Combine(launchProps.SocketDir, "helium.sock"),
                recorder,
                conf,
                artifact
            );
            
            var exitCode = await launcher.Run(currentPlatform, launchProps);

            await recorder.RecordMetadata();
            
            return exitCode;
        }

        private static ICleanup<Func<Task<LaunchProperties>>> GetDockerLaunchProps(PlatformInfo platform, List<SdkInfo> sdks, string workDir, string? currentDir, string sourcesDir, Config conf, ISdkInstallManager sdkInstallManager, BuildSchema schema) =>
            DirectoryCleanup.CreateTempDir(workDir, async tempDir => {

                var dockerImage = platform.OS switch {
                    SdkOperatingSystem.Linux => "helium-build/build-env:debian-buster-20190708",
                    SdkOperatingSystem.Windows => "helium-build/build-env:windows-servercore-1903",
                    _ => throw new Exception("Unexpected OS"),
                };

                var rootDir = platform.RootDirectory;
                
                
                var socketDir = Path.Combine(tempDir, "socket");
                Directory.CreateDirectory(socketDir);

                var installDir = Path.Combine(tempDir, "install");
                Directory.CreateDirectory(installDir);

                string? currentDirectory = null;
                if(currentDir != null) {
                    currentDirectory = platform.OS switch {
                        SdkOperatingSystem.Linux => Path.Combine("/sources/", currentDir),
                        SdkOperatingSystem.Windows => Path.Combine("C:\\sources\\", currentDir),
                        _ => throw new Exception("Unexpected OS"),
                    };
                }
                
                var props = new LaunchProperties(
                    dockerImage: dockerImage,
                    command: schema?.build?.command ?? throw new Exception("Build command not specified."),
                    sources: sourcesDir,
                    socketDir: socketDir,
                    installDir: installDir,
                    currentDirectory: currentDirectory
                );

                foreach(var requiredSdk in schema.sdk) {
                    if(requiredSdk.name == null) throw new Exception("Required sdk name is null.");
                    if(requiredSdk.version == null) throw new Exception("Required sdk version is null.");
                    
                    var sdk = sdks.FirstOrDefault(sdkInfo => sdkInfo.Matches(requiredSdk.name, requiredSdk.version) && sdkInfo.SupportedBy(platform));
                    if(sdk == null) {
                        throw new Exception($"Could not find match for sdk {requiredSdk.name} version {requiredSdk.version}");
                    }

                    var (sdkHash, sdkInstallDir) = await sdkInstallManager.GetInstalledSdkDir(sdk);

                    foreach(var (fileName, template) in sdk.ConfigFileTemplates) {
                        if(fileName.Contains(":")) {
                            throw new Exception("SDK config filenames may not contain colons.");
                        }

                        if(!PathUtil.IsValidSubPath(fileName)) {
                            throw new Exception("SDK config filenames may not contain . or .. directories");
                        }

                        var (baseDir, path) = GetConfigFilePath(fileName);

                        var fileContent = Template.Parse(template).Render(Hash.FromDictionary(conf.ToDictionary()));

                        var fullPath = Path.Combine(installDir, baseDir, path);
                        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                        await File.WriteAllTextAsync(fullPath, fileContent, Globals.HeliumEncoding);
                    }

                    var containerSdkDir = Path.Combine(rootDir, "helium/sdk", sdkHash);

                    foreach(var (name, envValue) in sdk.Env) {
                        props.Environment.TryAdd(name, envValue.Resolve(containerSdkDir));
                    }

                    props.PathDirs.AddRange(sdk.PathDirs.Select(dir => containerSdkDir + Path.DirectorySeparatorChar + dir));
                    props.SdkDirs.Add((containerSdkDir, sdkInstallDir));
                }

                return props;
            });

        private static (string baseDir, string path) GetConfigFilePath(string fileName) {
            if(fileName.StartsWith("~/")) {
                return ("home", fileName.Substring(2));
            }
            else if(fileName.StartsWith("$CONFIG/")) {
                return ("config", fileName.Substring(8));
            }
            else {
                throw new Exception("Invalid config path.");
            }
        }
        
    }
}