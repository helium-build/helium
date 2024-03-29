using System.Diagnostics;
using Helium.Engine.BuildExecutor.Protocol;

namespace Helium.Engine.Docker
{
    internal class DotNetRunLauncher : BuildExecutorCLILauncher
    {
        private readonly string projectDir;

        public DotNetRunLauncher(string sudoCommand, string projectDir) : base(sudoCommand, "dotnet") {
            this.projectDir = projectDir;
        }

        protected override void AddRunArguments(ProcessStartInfo psi, RunDockerCommand run) {
            psi.ArgumentList.Add("run");
            psi.ArgumentList.Add("--no-build");
            psi.ArgumentList.Add("--");
            base.AddRunArguments(psi, run);
        }
    }
}