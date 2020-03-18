using System.Security.Cryptography.X509Certificates;

namespace Helium.CI.Server
{
    public class ServerConfig
    {
        public ServerConfig(X509Certificate2 cert) {
            Cert = cert;
        }
        
        public X509Certificate2 Cert { get; }
    }
}