using System.Collections.Generic;

namespace Helium.Engine.Build
{
    public class BuildSchema
    {
        public BuildSpec? build { get; set; }
        
        public List<BuildSdk> sdk { get; set; } = new List<BuildSdk>();

        public class BuildSpec
        {
            public List<string>? command { get; set; }
        }

        public class BuildSdk
        {
            public string? name { get; set; }
            public string? version { get; set; }
        }

        public static BuildSchema Parse(string text) {
            throw new System.NotImplementedException();
        }
    }
}