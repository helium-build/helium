using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Helium.Util;
using Thrift.Transport;
using Thrift.Transport.Server;

namespace Helium.CI.Agent
{
    public class ServerTransportWorkspace : TTlsServerSocketTransport
    {
        public ServerTransportWorkspace(
            string workspacesDir,
            int port,
            X509Certificate2 certificate,
            RemoteCertificateValidationCallback? clientCertValidator = null,
            LocalCertificateSelectionCallback? localCertificateSelectionCallback = null,
            SslProtocols sslProtocols = SslProtocols.Tls12
        ) : base(port, certificate, clientCertValidator, localCertificateSelectionCallback, sslProtocols) {
            
            this.workspacesDir = workspacesDir;
        }
        
        private readonly string workspacesDir;
        
        
        protected override async ValueTask<TTransport> AcceptImplementationAsync(CancellationToken cancellationToken) {
            var workspace = DirectoryCleanup.CreateTempDir(workspacesDir);
            return new TransportBuildDir(workspace, await base.AcceptImplementationAsync(cancellationToken), cancellationToken);
        }
        
        
    }
}