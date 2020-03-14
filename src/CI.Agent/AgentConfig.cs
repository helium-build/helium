using System.Collections.Generic;

namespace Helium.CI.Agent
{
    public class AgentConfig
    {

        public List<CIServerInfo> CIServer { get; set; } = new List<CIServerInfo>();
        
        public class CIServerInfo
        {
            public string? PublicKey { get; set; }
        }
        
    }
}