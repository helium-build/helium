using System;
using System.IO;
using System.Threading.Tasks;
using Helium.Engine.Conf;
using Helium.Engine.Build.Record;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using static Helium.Env.Directories;

namespace Helium.Engine.Build.Proxy
{
    internal class ProxyServer : IAsyncDisposable
    {
        private ProxyServer(IHost host) {
            this.host = host;
        }
        
        private readonly IHost host;

        public async ValueTask DisposeAsync() {
            await host.StopAsync();
            host.Dispose();
        }

        public static async Task<ProxyServer> Create(string socketPath, IRecorder recorder, Config config, IArtifactSaver artifact) {
            var host = CreateHostBuilder(socketPath, recorder, config, artifact).Build();
            await host.StartAsync();
            return new ProxyServer(host);
        }

        private static IHostBuilder CreateHostBuilder(string socketPath, IRecorder recorder, Config config, IArtifactSaver artifact) =>
            new HostBuilder()
                .UseContentRoot(Path.GetFullPath(EngineContentRoot))
                .ConfigureLogging((hostingContext, logging) => {
                    logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                    logging.AddConsole();
                })
                .ConfigureWebHost(webBuilder => {
                    webBuilder
                        .ConfigureAppConfiguration(((builderContext, config) => {
                            var env = builderContext.HostingEnvironment;

                            config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                            config.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: false);
                        }))
                        .UseKestrel(options => {
                            options.ListenUnixSocket(Path.GetFullPath(socketPath));
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
                                MavenRoutes.Build(recorder, config).Register(endpoints);
                                NuGetRoutes.Build(recorder, artifact, config).Register(endpoints);
                                NpmRoutes.Build(recorder, artifact, config)?.Register(endpoints);
                                new ArtifactRoutes(artifact).Register(endpoints);
                            });
                        });
                });
        
    }
}