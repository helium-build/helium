using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Helium.Sdks;
using Helium.Engine.BuildExecutor.Protocol;

namespace Helium.Engine.Docker
{
    internal sealed class DockerCLILauncher : ProcessLauncher
    {
        public DockerCLILauncher(string? sudoCommand, string dockerCommand) : base(sudoCommand, dockerCommand) {
        }

        protected override void AddRunArguments(ProcessStartInfo psi, RunDockerCommand run) {
            psi.ArgumentList.Add("run");
            psi.ArgumentList.Add("--rm");

            psi.ArgumentList.Add("--network");
            psi.ArgumentList.Add("none");

            psi.ArgumentList.Add("--hostname");
            psi.ArgumentList.Add("helium-build-env");

            if(run.CurrentDirectory != null) {
                psi.ArgumentList.Add("--workdir");
                psi.ArgumentList.Add(run.CurrentDirectory);
            }

            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                psi.ArgumentList.Add("--isolation");
                psi.ArgumentList.Add("process");
            }

            foreach(var bindMount in run.BindMounts) {
                var mountSpec = bindMount.HostDirectory + ":" + bindMount.MountPath;
                if(bindMount.IsReadOnly) mountSpec += ":ro";
                
                psi.ArgumentList.Add("-v");
                psi.ArgumentList.Add(mountSpec);
            }

            foreach(var (name, value) in run.Environment) {
                psi.ArgumentList.Add("-e");
                psi.ArgumentList.Add($"{name}={value}");
            }

            psi.ArgumentList.Add(run.ImageName);

            run.Command.ForEach(psi.ArgumentList.Add);
        }

        protected override void AddContainerBuildArguments(ProcessStartInfo psi, RunDockerBuild build) =>
            throw new NotSupportedException();

    }
}