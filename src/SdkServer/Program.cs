using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Helium.Sdks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SdkServer
{
    public class Program
    {
        public static async Task Main(string[] args) {
            var resolver = await SdkResolver.Build(SdkLoader.LoadSdks("sdks/"));
            await CreateHostBuilder(resolver).Build().RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(ISdkResolver resolver) =>
            new HostBuilder()
                .UseContentRoot(".")
                .ConfigureLogging((hostingContext, logging) => {
                    logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                    logging.AddConsole();
                })
                .ConfigureWebHost(webBuilder => {
                    webBuilder
                        .ConfigureAppConfiguration((builderContext, config) => {
                            var env = builderContext.HostingEnvironment;

                            config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                            config.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true,
                                reloadOnChange: false);
                        })
                        .ConfigureServices(services => {
                            services.AddRouting();
                        })
                        .Configure(app => {
                            app.UseEndpoints(new Endpoints(resolver).Register);
                        });
                });
    }
}
