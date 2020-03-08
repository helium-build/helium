using System.Diagnostics;
using Helium.JobExecutor;

namespace Engine.Docker
{
    internal class DotNetRunLauncher : JobExecutorCLILauncher
    {
        private readonly string projectDir;

        public DotNetRunLauncher(string sudoCommand, string projectDir) : base(sudoCommand, "dotnet") {
            this.projectDir = projectDir;
        }

        protected override void AddRunArguments(ProcessStartInfo psi, JobExecutorProtocol.RunDockerCommand run) {
            psi.ArgumentList.Add("run");
            psi.ArgumentList.Add("--no-build");
            psi.ArgumentList.Add("--");
            base.AddRunArguments(psi, run);
        }
    }
}