using System.Security.Cryptography.X509Certificates;
using JsonSubTypes;
using Newtonsoft.Json;

namespace Helium.CI.Server
{
    public sealed class AgentConfig
    {
        public AgentConfig(string name, int workers, AgentConnection connection) {
            Name = name;
            Workers = workers;
            Connection = connection;
        }
        
        public string Name { get; }
        public int Workers { get; }
        public AgentConnection Connection { get; }
    }
    
    [JsonConverter(typeof(JsonSubtypes), "Type")]
    [JsonSubtypes.KnownSubType(typeof(SslAgentConnection), "ssl")]
    public abstract class AgentConnection
    {
        internal AgentConnection() {}
        
        public abstract string Type { get; }
    }

    public sealed class SslAgentConnection : AgentConnection
    {
        public SslAgentConnection(string host, int port, X509Certificate2 agentCert) {
            Host = host;
            Port = port;
            AgentCert = agentCert;
        }

        [JsonConstructor]
        public SslAgentConnection(string host, int port, byte[] agentCert)
            : this(
                host: host,
                port: port,
                agentCert: new X509Certificate2(agentCert)
        ) {}

        public override string Type => "ssl";

        public string Host { get; }
        
        public int Port { get; }
        
        [JsonIgnore]
        public X509Certificate2 AgentCert { get; }

        [JsonProperty("AgentCert")]
        public byte[] AgentCertBytes => AgentCert.Export(X509ContentType.Cert);
    }
}