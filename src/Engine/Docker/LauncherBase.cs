using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Helium.Engine.Docker;
using Helium.Sdks;
using static Helium.JobExecutor.JobExecutorProtocol;

namespace Engine.Docker
{
    internal abstract class LauncherBase : ILauncher
    {
        public abstract Task<int> Run(PlatformInfo platform, LaunchProperties props);
        public abstract Task<int> BuildContainer(PlatformInfo platform, Func<Stream, Task> buildContext);


        protected RunDockerCommand BuildRunCommand(PlatformInfo platform, LaunchProperties props) {
            var rootFSPath = platform.RootDirectory;

            var run = new RunDockerCommand {
                ImageName = props.DockerImage,
                Command = props.Command,
                Environment = new Dictionary<string, string>(props.Environment) {
                    ["HELIUM_SDK_PATH"] = string.Join(Path.PathSeparator, props.PathDirs)
                },
            };
            
            run.BindMounts.Add(new DockerBindMount {
                HostDirectory = Path.GetFullPath(props.SocketDir),
                MountPath = rootFSPath + "helium/socket",
            });

            foreach(var (containerDir, hostDir) in props.SdkDirs) {
                run.BindMounts.Add(new DockerBindMount {
                    HostDirectory = Path.GetFullPath(hostDir),
                    MountPath = containerDir,
                    IsReadOnly = true,
                });
            }

            run.BindMounts.Add(new DockerBindMount {
                HostDirectory = Path.GetFullPath(props.Sources),
                MountPath = rootFSPath + "sources",
            });

            run.BindMounts.Add(new DockerBindMount {
                HostDirectory = Path.GetFullPath(props.InstallDir),
                MountPath = rootFSPath + "helium/install",
            });

            return run;
        }
    }
}