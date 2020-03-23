using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Helium.Engine.Docker;
using Helium.Sdks;
using Helium.Util;
using Newtonsoft.Json;
using Helium.Engine.BuildExecutor.Protocol;

namespace Helium.Engine.Docker
{
    internal class BuildExecutorCLILauncher : ProcessLauncher
    {
        public BuildExecutorCLILauncher(string? sudoCommand, string dockerCommand) : base(sudoCommand, dockerCommand) {
        }

        protected override void AddRunArguments(ProcessStartInfo psi, RunDockerCommand run) {
            psi.ArgumentList.Add("run");
            psi.ArgumentList.Add(JsonConvert.SerializeObject(run));
        }

        protected override void AddContainerBuildArguments(ProcessStartInfo psi, RunDockerBuild build) {
            psi.ArgumentList.Add("container-build");
            psi.ArgumentList.Add(JsonConvert.SerializeObject(build));
        }
    }
}