using System;
using System.Collections.Generic;
using System.Linq;
using Nett;

namespace Helium.Engine.Conf
{
    public sealed class Config
    {
        public Repos repos { get; set; } = new Repos();
        
        public Dictionary<string, object> ToDictionary() => new Dictionary<string, object> {
            { "repos", repos.ToDictionary() },
        };
    }
}