using System;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Thrift.Transport;
using Thrift.Transport.Client;

namespace Helium.CI.Server
{
    class SslAgentManager : AgentManager
    {
        public SslAgentManager(IJobQueue jobQueue, X509Certificate2 cert, X509Certificate2 agentCert) : base(jobQueue) {
            this.cert = cert;
            this.agentCert = agentCert;
        }

        private readonly X509Certificate2 cert;
        private readonly X509Certificate2 agentCert;
        
        protected override TTransport CreateTransport() {
            return new TTlsSocketTransport(IPAddress.Loopback, 8080, cert, certValidator: CertValidator);
        }

        private bool CertValidator(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors policyErrors) =>
            certificate.Equals(agentCert);

    }
}