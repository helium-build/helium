using System.Collections.Generic;
using System.Linq;
using Helium.DockerfileHandler.Commands;

namespace Helium.DockerfileHandler
{
    public class DockerfileBuild
    {
        public DockerfileBuild(FromCommand fromCommand, IEnumerable<CommandBase> commands) {
            FromCommand = fromCommand;
            Commands = commands.ToList().AsReadOnly();
        }
        
        public FromCommand FromCommand { get; }
        public IReadOnlyList<CommandBase> Commands { get; }
    }
}