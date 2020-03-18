using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Helium.Util;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using static Helium.Env.Directories;

namespace Helium.CI.Server.UI
{
    public class Program
    {
        public static async Task Main(string[] args) {
            var cancel = new CancellationTokenSource();
            var jobQueue = new JobQueue();

            using var cert = await CertUtil.LoadOrGenerateCertificate(Path.Combine(ConfDir, "cert.pfx"));
            
            Console.WriteLine("Helium CI Agent");
            Console.WriteLine("TLS Key");
            Console.WriteLine(Convert.ToBase64String(cert.Export(X509ContentType.Cert)));

            var serverConfig = new ServerConfig(cert);

            var agentManager = await AgentManager.Load(Path.Combine(ConfDir, "agents"), jobQueue, serverConfig, cancel.Token);
            var projectManager = await ProjectManager.Load(Path.Combine(ConfDir, "projects"), jobQueue, cancel.Token);
            
            try {
                await CreateHostBuilder(agentManager, projectManager).Build().RunAsync();
            }
            finally {
                cancel.Cancel();
            }
        }

        public static IHostBuilder CreateHostBuilder(IAgentManager agentManager, IProjectManager projectManager) =>
            new HostBuilder()
                .UseContentRoot(Path.GetFullPath(AgentContentRoot))
                .ConfigureLogging((hostingContext, logging) => {
                    logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                    logging.AddConsole();
                })
                .ConfigureWebHost(webBuilder => {
                    webBuilder
                        .ConfigureAppConfiguration(((builderContext, config) => {
                            var env = builderContext.HostingEnvironment;

                            config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                            config.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true,
                                reloadOnChange: false);
                        }))
                        .UseKestrel(options => {

                        })
                        .ConfigureServices(services => {
                            services.AddRazorPages();
                            services.AddRouting();
                            services.AddServerSideBlazor();
                            services.AddLogging();
                            services.AddSingleton<IAgentManager>(_ => agentManager);
                            services.AddSingleton<IProjectManager>(_ => projectManager);
                        })
                        .Configure(app => {
                            var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
                            
                            if (env.IsDevelopment())
                            {
                                app.UseDeveloperExceptionPage();
                            }

                            app.UseStaticFiles();
                            
                            app.UseRouting();
                            

                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapBlazorHub();
                                endpoints.MapFallbackToPage("/_Host");
                            });
                        });
                });
    }
}
