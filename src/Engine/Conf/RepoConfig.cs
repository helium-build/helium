using System;
using System.Collections.Generic;
using System.Linq;
using Nett;

namespace Helium.Engine.Conf
{
    public sealed class RepoConfig
    {
        public Repos repo { get; set; } = new Repos();
        
        public Dictionary<string, object> ToDictionary() => new Dictionary<string, object> {
            { "repo", repo.ToDictionary() },
        };

        public static RepoConfig Parse(string text) =>
            Toml.ReadString<RepoConfig>(text);
    }
}