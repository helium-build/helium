using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using DotLiquid;
using Helium.Engine.Build;
using Helium.Engine.Cache;
using Helium.Engine.Conf;
using Helium.Engine.Docker;
using Helium.Engine.Record;
using Helium.Sdks;
using Helium.Util;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Helium.Engine
{
    internal static class BuildManager
    {
        public static async Task RunBuild(Func<Task<IRecorder>> createRecorder, string outputDir, string workDir) {
            using var recorder = await createRecorder();

            var artifact = new FSArtifactSaver(outputDir);

            var schema = await recorder.LoadSchema();

            var sdks = await recorder.ListAvailableSdks().ToListAsync();
            var sdkInstallManager = await recorder.CreateSdkInstaller();

            var conf = await recorder.LoadRepoConfig();

            using var launchPropsCleanup = GetDockerLaunchProps(
                sdks: sdks,
                workDir: workDir,
                sourcesDir: recorder.SourcesDir,
                conf,
                sdkInstallManager,
                schema
            );

            var launchProps = await launchPropsCleanup.Value();


        }

        private static ICleanup<Func<Task<LaunchProperties>>> GetDockerLaunchProps(List<SdkInfo> sdks, string workDir, string sourcesDir, RepoConfig conf, SdkInstallManager sdkInstallManager, BuildSchema schema) =>
            DirectoryCleanup.CreateTempDir(workDir, async tempDir => {

                var currentPlatform = PlatformInfo.Current;

                var dockerImage = currentPlatform.os switch {
                    SdkOperatingSystem.Linux => "helium-build/build-env:debian-buster-20190708",
                    SdkOperatingSystem.Windows => "helium-build/build-env:windows-nanoserver-1903",
                    _ => throw new Exception("Unexpected OS"),
                };

                var rootDir = currentPlatform.os switch {
                    SdkOperatingSystem.Linux => "/",
                    SdkOperatingSystem.Windows => "C:\\",
                    _ => throw new Exception("Unexpected OS"),
                };
                
                
                var socketDir = Path.Combine(tempDir, "socket");
                Directory.CreateDirectory(socketDir);

                var installDir = Path.Combine(tempDir, "install");
                Directory.CreateDirectory(installDir);
                
                var props = new LaunchProperties(
                    dockerImage: dockerImage,
                    command: schema?.build?.command ?? throw new Exception("Build command not specified."),
                    sources: sourcesDir,
                    socketDir: Path.Combine(socketDir, "helium.sock"),
                    installDir: installDir
                );

                foreach(var requiredSdk in schema.sdk) {
                    var sdk = sdks.First(sdkInfo => sdkInfo.Matches(requiredSdk.name, requiredSdk.version) && sdkInfo.SupportedBy(currentPlatform));

                    var (sdkHash, sdkInstallDir) = await sdkInstallManager.GetInstalledSdkDir(sdk);

                    foreach(var (fileName, template) in sdk.configFileTemplates) {
                        if(fileName.Contains(":")) {
                            throw new Exception("SDK config filenames may not contain colons.");
                        }

                        if(fileName.Split('/', '\\').Any(seg => seg == "." || seg == "..")) {
                            throw new Exception("SDK config filenames may not contain . or .. directories");
                        }

                        var (baseDir, path) = GetConfigFilePath(fileName);

                        var fileContent = Template.Parse(template).Render(Hash.FromDictionary(conf.ToDictionary()));
                        await File.WriteAllTextAsync(Path.Combine(installDir, baseDir, path), fileContent, Encoding.UTF8);
                    }

                    var containerSdkDir = Path.Combine(rootDir, "helium/sdk", sdkHash);

                    foreach(var (name, envValue) in sdk.env) {
                        props.Environment.TryAdd(name, envValue.Resolve(containerSdkDir));
                    }

                    props.PathDirs.AddRange(sdk.pathDirs.Select(dir => containerSdkDir + Path.DirectorySeparatorChar + dir));
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

        //CreateHostBuilder().Build().Run();

        private static IHostBuilder CreateHostBuilder() =>
            new HostBuilder()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .ConfigureLogging((hostingContext, logging) => {
                    logging.AddConfiguration((IConfiguration) hostingContext.Configuration.GetSection("Logging"));
                    logging.AddConsole();
                    logging.AddDebug();
                    logging.AddEventSourceLogger();
                })
                .ConfigureWebHost(webBuilder => {
                    webBuilder
                        .UseSetting(WebHostDefaults.SuppressStatusMessagesKey, "True")
                        .ConfigureAppConfiguration(((builderContext, config) => {
                            var env = builderContext.HostingEnvironment;

                            config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                            config.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: false);
                        }))
                        .UseKestrel(options => {
                            options.ListenUnixSocket(Path.Combine(Directory.GetCurrentDirectory(), "test.sock"));
                        })
                        .ConfigureServices(services => {
                            services.AddRouting();
                        })
                        .Configure(app => {
                            var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();

                            if(env.IsDevelopment()) {
                                app.UseDeveloperExceptionPage();
                            }

                            app.UseRouting();

                            app.UseEndpoints(endpoints => {
                                endpoints.MapGet("/", async context => {
                                    await context.Response.WriteAsync("Hello World!");
                                });
                            });
                        });
                });
    }
}