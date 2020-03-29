using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Helium.DockerfileHandler.Commands
{
    public class RunShellCommand : CommandBase
    {
        public RunShellCommand(string shellCommand, IReadOnlyDictionary<string, string> buildArgs) {
            ShellCommand = shellCommand;
            BuildArgs = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(buildArgs));
        }

        public override string CommandName => "RUN_SHELL";
        
        public string ShellCommand { get; }
        public ReadOnlyDictionary<string, string> BuildArgs { get; }
    }
}