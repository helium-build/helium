using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Helium.Engine.Docker;
using Helium.Sdks;
using Helium.Util;
using Newtonsoft.Json;
using static Helium.JobExecutor.JobExecutorProtocol;

namespace Engine.Docker
{
    internal class JobExecutorCLILauncher : ProcessLauncher
    {
        public JobExecutorCLILauncher(string? sudoCommand, string dockerCommand) : base(sudoCommand, dockerCommand) {
        }

        protected override void AddRunArguments(ProcessStartInfo psi, RunDockerCommand run) {
            psi.ArgumentList.Add("run");
            psi.ArgumentList.Add(JsonConvert.SerializeObject(run));
        }

        public override async Task<int> BuildContainer(PlatformInfo platform,  Func<Stream, Task> buildContext) {
            var psi = CreatePSI();
            psi.ArgumentList.Add("build-container");
            psi.ArgumentList.Add(JsonConvert.SerializeObject(new RunDockerBuild {
                ProxyImage = "helium/container-build-proxy",
            }));

            psi.RedirectStandardInput = true;

            var p = Process.Start(psi);

            if(p == null) {
                throw new Exception("Could not start process.");
            }

            await buildContext(p.StandardInput.BaseStream);
            await p.WaitForExitAsync();

            return p.ExitCode;
        }
    }
}