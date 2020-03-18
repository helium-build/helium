using System.ComponentModel.DataAnnotations;

namespace Helium.CI.Server.UI
{
    public sealed class AgentAddModel
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
        
    }
}