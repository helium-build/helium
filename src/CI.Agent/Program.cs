using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Helium.CI.Common.Protocol;
using Helium.Util;
using Microsoft.Extensions.Logging;
using Nett;
using Thrift.Processor;
using Thrift.Protocol;
using Thrift.Server;
using Thrift.Transport;
using Thrift.Transport.Server;
using static Helium.Env.Directories;

namespace Helium.CI.Agent
{
    internal class Program
    {
        private static async Task Main(string[] args) {
            var logger = LoggerFactory.Create(builder => {
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddConsole();
            }).CreateLogger("helium-agent");
            
            
            using var cert = await CertUtil.LoadOrGenerateCertificate(Path.Combine(ConfDir, "cert.pfx"));

            var config = await LoadConfig();
            var allowedCerts = config.CIServer
                .Select(ciServer => ciServer.PublicKey)
                .Where(key => key != null)
                .Select(key => new X509Certificate2(Convert.FromBase64String(key!)))
                .ToList();
            
            Console.WriteLine("Helium CI Agent");
            Console.WriteLine("TLS Key");
            Console.WriteLine(Convert.ToBase64String(cert.Export(X509ContentType.Cert)));
            
            var transport = new TTlsServerSocketTransport(8080, cert, clientCertValidator: ValidateCert(allowedCerts));
            var server = new TThreadPoolAsyncServer(
                new BuildAgentFactory(),
                transport,
                null,
                null,
                new TBinaryProtocol.Factory(),
                new TBinaryProtocol.Factory(),
                new TThreadPoolAsyncServer.Configuration(),
                logger
            );

            var cancel = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => {
                if(!cancel.IsCancellationRequested) {
                    e.Cancel = true;
                    transport.Close();
                    cancel.Cancel();
                }
            };

            
            await server.ServeAsync(cancel.Token);
        }
        private static async Task<AgentConfig> LoadConfig() {
            var file = Path.Combine(ConfDir, "agent.toml");
            return Toml.ReadString<AgentConfig>(await File.ReadAllTextAsync(file));
        }

        private static RemoteCertificateValidationCallback ValidateCert(IEnumerable<X509Certificate2> allowedCerts) => (sender, certificate, chain, policyErrors) =>
            allowedCerts.Any(certificate.Equals);
    }
}
