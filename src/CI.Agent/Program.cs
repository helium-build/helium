using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using CI.Common;
using Grpc.Core;
using Helium.CI.Common;
using Helium.Util;
using Microsoft.Extensions.Logging;
using Nett;
using Nito.AsyncEx;
using static Helium.Env.Directories;

namespace Helium.CI.Agent
{
    internal class Program
    {
        private static async Task<int> Main(string[] args) {

            string hostname = RequireEnvValue("HELIUM_CI_SERVER_HOST");
            int port = RequireEnvValueInt("HELIUM_CI_SERVER_PORT");
            string apiKey = RequireEnvValue("HELIUM_CI_AGENT_KEY");
            int maxJobs =
                Environment.GetEnvironmentVariable("HELIUM_CI_AGENT_MAX_JOBS") is {} maxJobStr &&
                int.TryParse(maxJobStr, out var maxJobsInt)
                    ? maxJobsInt
                    : 1;


            var logger = LoggerFactory.Create(builder => {
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddConsole();
            }).CreateLogger("helium-agent");

            foreach(var dir in Directory.GetDirectories(AgentWorkspacesDir)) {
                Directory.Delete(dir, recursive: true);
            }
            
            Console.WriteLine("Helium CI Agent");


            Channel? channel = null;
            
            var cancel = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => {
                if(!cancel.IsCancellationRequested) {
                    e.Cancel = true;
                    cancel.Cancel();
                    channel?.ShutdownAsync();
                }
            };

            channel = new Channel(hostname, port, ChannelCredentials.Insecure);
            try {
                var client = new BuildServer.BuildServerClient(channel);
                var runner = new BuildAgent(logger, client, apiKey, AgentWorkspacesDir, maxJobs);
                await runner.JobLoop(cancel.Token);
            }
            catch(OperationCanceledException) {}
            finally {
                try {
                    await channel.ShutdownAsync();
                }
                catch {}
            }
            
            
            return 0;
        }

        private static string RequireEnvValue(string envName) =>
            Environment.GetEnvironmentVariable(envName) ?? throw new System.Exception($"Environment variable {envName} was unspecified.");

        private static int RequireEnvValueInt(string envName) =>
            int.TryParse(RequireEnvValue(envName), out var value)
                ? value
                : throw new System.Exception($"Environment variable {envName} must be an integer.");
    }
}
