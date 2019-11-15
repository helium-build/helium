using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Helium.Engine
{
    public static class Program
    {
        public static void Main(string[] args) {
            var host = CreateHostBuilder().Build();
            host.Run();
        }

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
                        .UseStartup<Startup>();
                });
    }
}
