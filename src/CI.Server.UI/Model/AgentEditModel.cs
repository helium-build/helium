using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;

namespace Helium.CI.Server.UI
{
    public sealed class AgentEditModel
    {
        public AgentEditModel(string key) {
            Key = key;
        }
        
        [Required]
        public string? Name { get; set; }

        public string Key { get; }

        internal AgentConfig? ToConfig() {
            return new AgentConfig(Name!, Key);
        }
        
    }
}