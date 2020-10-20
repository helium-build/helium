using System.Security.Cryptography.X509Certificates;
using JsonSubTypes;
using Newtonsoft.Json;

namespace Helium.CI.Server
{
    public sealed class AgentConfig
    {
        public AgentConfig(string name, string key) {
            Name = name;
            Key = key;
        }
        
        public string Name { get; }
        public string Key { get; }
    }
}