using System;
using System.Collections.Generic;

namespace Helium.Engine.Conf
{
    public sealed class MavenRepo
    {
        public string? name { get; set; }
        public string? url { get; set; }
        
        public Dictionary<string, object?> ToDictionary() {
            var name = this.name ?? throw new Exception("Maven repo name is missing.");
            return new Dictionary<string, object?> {
                {"name", name},
                {"url", $"http://localhost:9000/maven/{name}/"},
            };
        }
    }
}