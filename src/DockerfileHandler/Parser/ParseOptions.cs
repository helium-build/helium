using System.Collections.Generic;

namespace Helium.DockerfileHandler.Parser
{
    public class ParseOptions
    {
        public ParseOptions(IReadOnlyDictionary<string, string> buildArgs) {
            BuildArgs = buildArgs;
        }

        public bool LookForDirectives { get; set; } = true;
        public bool EscapeSeen { get; set; } = false;
        public char EscapeChar { get; set; } = '\\';
        
        public IReadOnlyDictionary<string, string> BuildArgs { get; set; }
    }
}