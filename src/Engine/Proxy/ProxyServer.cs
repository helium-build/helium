using System;
using System.IO;
using System.Threading.Tasks;
using Helium.Engine.Record;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Helium.Engine.Proxy
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

        public static async Task<ProxyServer> Create(string socketPath, IRecorder recorder, IArtifactSaver artifact) {
            var host = CreateHostBuilder(socketPath, recorder, artifact).Build();
            await host.StartAsync();
            return new ProxyServer(host);
        }

        private static IHostBuilder CreateHostBuilder(string socketPath, IRecorder recorder, IArtifactSaver artifact) =>
            new HostBuilder()
                .UseContentRoot(Program.AppDir)
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
                            options.ListenUnixSocket(socketPath);
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