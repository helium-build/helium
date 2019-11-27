using System.Diagnostics;
using Newtonsoft.Json;
using static Helium.JobExecutor.JobExecutorProtocol;

namespace Engine.Docker
{
    internal class JobExecutorCLILauncher : ProcessLauncher
    {
        public JobExecutorCLILauncher(string? sudoCommand, string dockerCommand) : base(sudoCommand, dockerCommand) {
        }

        protected override void AddArguments(ProcessStartInfo psi, RunDockerCommand run) {
            psi.ArgumentList.Add("run");
            psi.ArgumentList.Add(JsonConvert.SerializeObject(run));
        }
    }
}