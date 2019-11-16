using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using Helium.Engine.Record;
using Helium.Util;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

internal static class BuildManager
{
    public static async Task RunBuild(Func<Task<IRecorder>> createRecorder, string outputDir, string workDir) {
        using var recorder = await createRecorder();
        
        var artifact = new FSArtifactSaver(outputDir);
        
        var schema = await recorder.LoadSchema();

        var sdks = await recorder.ListAvailableSdks().ToListAsync();
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
                        config.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true,
                            reloadOnChange: false);
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