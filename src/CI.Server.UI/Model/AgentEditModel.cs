using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;

namespace Helium.CI.Server.UI
{
    public sealed class AgentEditModel
    {
        [Required]
        public string? Name { get; set; }

        [Required]
        [Range(1, AgentExecutor.MaxWorkers)]
        public int Workers { get; set; } = 1;

        [Required]
        public string ConnectionType { get; set; } = "ssl";
        
        [RequiredIfChoice(propertyName: nameof(ConnectionType), desiredValue: "ssl")]
        public string? SslHost { get; set; }
        
        [RequiredIfChoice(propertyName: nameof(ConnectionType), desiredValue: "ssl")]
        public int SslPort { get; set; }
        
        [RequiredIfChoice(propertyName: nameof(ConnectionType), desiredValue: "ssl")]
        public string? AgentSslKey { get; set; }

        internal AgentConfig? ToConfig() {
            var conn = ConnectionType switch {
                "ssl" => new SslAgentConnection(SslHost!, SslPort, Convert.FromBase64String(AgentSslKey!)),
                _ => null
            };

            if(conn == null) return null;

            return new AgentConfig(Name!, Workers, conn);
        }
        
    }
}