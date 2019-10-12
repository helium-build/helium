package dev.helium_build.docker

import java.io.File

import zio.{IO, Task, ZIO}

object Launcher {

  private def buildEnvArgs(path: Seq[String], env: Map[String, String]): Seq[String] =
    env.updated("PATH", path.mkString(":"))
      .toSeq
      .flatMap { case (name, value) => Seq("-e", s"$name=$value") }

  private def buildSdkVolumes(sdkPaths: Seq[(String, File)]): Seq[String] =
    sdkPaths.flatMap { case (containerDir, dir) => Seq("-v", dir.getCanonicalPath + ":" + containerDir + ":ro") }

  def run(props: LaunchProperties): Task[Unit] =
    IO.effect {
      new ProcessBuilder(
        dockerCommand ++
          Seq("run", "--rm") ++
          Seq("--network", "none", "--hostname", "helium-build-env") ++
          props.sockets.flatMap {
            case (outName, inName) =>
              Seq("-v", outName + ":" + inName)
          } ++
          buildSdkVolumes(props.sdkDirs) ++
          buildEnvArgs(props.pathDirs, props.env) ++
          Seq("-v", props.workDir.getAbsolutePath + ":/work/") ++
          props.configFiles
            .map {
              case (outName, inName) if inName startsWith "~/" =>
                (outName, "/helium/install/home" + inName.substring(1))

              case (outName, inName) if inName startsWith "$CONFIG/" =>
                (outName, "/helium/install/home/.config" + inName.substring(7))

              case (outName, inName) if inName startsWith "/" =>
                (outName, "/helium/install/root" + inName)

              case (_, _) =>
                throw new RuntimeException("Invalid config path.")
            }
            .distinctBy {
              case (_, inName) => inName
            }
            .flatMap {
              case (outName, inName) =>
                Seq("-v", outName + ":" + inName)
            } ++
          Seq("helium-build/build-env:debian-buster-20190708", "env") ++
          props.command
      : _*)
        .inheritIO()
        .start()
    }
    .flatMap(ProcessHelpers.waitForExit)

  private def dockerCommand: Seq[String] =
    if(sys.env.contains("HELIUM_DEV_MODE"))
      Seq("sudo", "docker")
    else
      Seq("/helium/docker-launcher")

}
