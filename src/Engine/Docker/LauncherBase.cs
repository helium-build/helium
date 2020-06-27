using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Helium.Engine.Docker;
using Helium.Sdks;
using Helium.Engine.BuildExecutor.Protocol;

namespace Helium.Engine.Docker
{
    internal abstract class LauncherBase : ILauncher
    {
        public abstract Task<int> Run(PlatformInfo platform, LaunchProperties props);
        public abstract Task<int> BuildContainer(PlatformInfo platform, RunDockerBuild props);


        protected RunDockerCommand BuildRunCommand(PlatformInfo platform, LaunchProperties props) {
            var rootFSPath = platform.RootDirectory;

            var mounts = new List<DockerBindMount>();

            mounts.Add(new DockerBindMount(
                hostDirectory: Path.GetFullPath(props.SocketDir),
                mountPath: rootFSPath + "helium/socket"
            ));

            foreach(var (containerDir, hostDir) in props.SdkDirs) {
                mounts.Add(new DockerBindMount(
                    hostDirectory: Path.GetFullPath(hostDir),
                    mountPath: containerDir,
                    isReadOnly: true
                ));
            }

            mounts.Add(new DockerBindMount(
                hostDirectory: Path.GetFullPath(props.Sources),
                mountPath: rootFSPath + "sources"
            ));

            mounts.Add(new DockerBindMount(
                hostDirectory: Path.GetFullPath(props.InstallDir),
                mountPath: rootFSPath + "helium/install"
            ));
            
            var run = new RunDockerCommand(
                imageName: props.DockerImage,
                command: props.Command,
                currentDirectory: props.CurrentDirectory,
                environment: new Dictionary<string, string>(props.Environment) {
                    ["HELIUM_SDK_PATH"] = string.Join(Path.PathSeparator, props.PathDirs)
                },
                bindMounts: mounts
            );
            
            return run;
        }
    }
}