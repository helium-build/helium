using System.Collections.Generic;
using System.Linq;

namespace Helium.Engine.Conf
{
    public sealed class Repos
    {
        public List<MavenRepo> maven { get; set; } = new List<MavenRepo>();
        public List<NuGetRepo> nuget { get; set; } = new List<NuGetRepo>();
        public NpmRepo? npm { get; set; } = null;
        
        public Dictionary<string, object?> ToDictionary() => new Dictionary<string, object?> {
            { "maven", maven.Select(repo => repo.ToDictionary()).ToArray() },
            { "nuget", nuget.Select(repo => repo.ToDictionary()).ToArray() },
            { "nuget_push_url", "http://localhost:9000/nuget/publish" },
            { "npm", npm?.ToDictionary() },
        };
    }
}