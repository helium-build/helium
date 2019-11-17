using System.Collections.Generic;

namespace Helium.Engine.Conf
{
    public sealed class NpmRepo
    {
        public string? registry { get; set; } 
        
        public Dictionary<string, object?> ToDictionary() => new Dictionary<string, object?> {
            {"registry", "http://localhost:9000/npm/"}
        };
    }
}