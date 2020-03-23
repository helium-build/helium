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
        public abstract Task<int> BuildContainer(PlatformInfo platform, ContainerBuildProperties props);


        protected RunDockerCommand BuildRunCommand(PlatformInfo platform, LaunchProperties props) {
            var rootFSPath = platform.RootDirectory;

            var run = new RunDockerCommand {
                ImageName = props.DockerImage,
                Command = props.Command,
                Environment = new Dictionary<string, string>(props.Environment) {
                    ["HELIUM_SDK_PATH"] = string.Join(Path.PathSeparator, props.PathDirs)
                },
            };

            if(props.CurrentDirectory != null) {
                run.CurrentDirectory = props.CurrentDirectory;
            }
            
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

        protected RunDockerBuild BuildContainerBuildCommand(PlatformInfo platform, ContainerBuildProperties props) =>
            new RunDockerBuild {
                ProxyImage = "helium/container-build-proxy",
                BuildArgs = new Dictionary<string, string>(props.BuildArgs), 
                OutputFile = props.OutputFile + ".tmp",
                CacheDirectory = props.CacheDir,
                WorkspaceTar = props.WorkspaceTar,
            };
    }
}