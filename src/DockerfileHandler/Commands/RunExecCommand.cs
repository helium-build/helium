using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Helium.DockerfileHandler.Commands
{
    public class RunExecCommand : CommandBase
    {
        public RunExecCommand(IEnumerable<string> execCommand, IReadOnlyDictionary<string, string> buildArgs) {
            ExecCommand = execCommand.ToList().AsReadOnly();
            BuildArgs = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(buildArgs));
        }

        public override string CommandName => "RUN_EXEC";
        
        public IReadOnlyList<string> ExecCommand { get; }

        public ReadOnlyDictionary<string, string> BuildArgs { get; }
    }
}