package dev.helium_build.docker

import java.io.File

import org.apache.commons.lang3.SystemUtils
import sttp.client._
import sttp.model._
import sttp.client.asynchttpclient.zio.{AsyncHttpClientZioBackend, ZioWebSocketHandler}
import sttp.model.ws.WebSocketFrame
import zio.blocking.Blocking
import zio._

object Launcher {

  private def buildEnvArgs(path: Seq[String], env: Map[String, String]): Seq[String] =
    env.updated("HELIUM_SDK_PATH", path.mkString(":"))
      .toSeq
      .flatMap { case (name, value) => Seq("-e", s"$name=$value") }

  private def buildSdkVolumes(sdkPaths: Seq[(String, File)]): Seq[String] =
    sdkPaths.flatMap { case (containerDir, dir) => Seq("-v", dir.getCanonicalPath + ":" + containerDir + ":ro") }

  def run(props: LaunchProperties): Task[Unit] =
    IO.effectTotal { sys.env.get("HELIUM_DOCKER_WS_PROXY") }.flatMap {
      case Some(proxyUrl) => runWSClient(proxyUrl, props)
      case None => runProcess(props)
    }

  private def runProcess(props: LaunchProperties): Task[Unit] =
    IO.effect {

      val rootFSPath =
        if(SystemUtils.IS_OS_WINDOWS) "C:/"
        else "/"

      val command = (
        dockerCommand ++
          Seq("run", "--rm") ++
          Seq("--network", "none", "--hostname", "helium-build-env") ++
          Seq("-v", s"${props.socketDir.getAbsolutePath}:${rootFSPath}helium/socket/") ++
          buildSdkVolumes(props.sdkDirs) ++
          buildEnvArgs(props.pathDirs, props.env) ++
          Seq("-v", s"${props.sourcesDir.getAbsolutePath}:${rootFSPath}sources/") ++
          Seq("-v", s"${props.installDir.getAbsolutePath}:${rootFSPath}helium/install/") ++
          Seq(props.dockerImage) ++
          props.command
      )

      new ProcessBuilder(command: _*)
        .inheritIO()
        .start()
    }
    .flatMap(ProcessHelpers.waitForExit)

  private def runWSClient(clientUrl: String, props: LaunchProperties): Task[Unit] =
    AsyncHttpClientZioBackend().flatMap { implicit sttpBackend =>
      ZioWebSocketHandler().flatMap { socketHandler =>
        basicRequest
          .get(uri"$clientUrl")
          .openWebsocket(socketHandler)
      }
    }
      .flatMap { response =>
        val ws = response.result

        def consumeOutput: RIO[Blocking, Int] =
          ws.receiveData().flatMap {
            case Left(_) => IO.succeed(1)
            case Right(WebSocketFrame.Text(payload, _, _)) =>
              ???

            case Right(WebSocketFrame.Binary(payload, _, _)) if payload.isEmpty => consumeOutput

            case Right(WebSocketFrame.Binary(payload, _, _)) if payload(0) == 0 =>
              ZIO.accessM[Blocking] { _.blocking.effectBlocking {
                System.out.write(payload, 1, payload.length - 1)
              } } *> consumeOutput

            case Right(WebSocketFrame.Binary(payload, _, _)) =>
              ZIO.accessM[Blocking] { _.blocking.effectBlocking {
                System.err.write(payload, 1, payload.length - 1)
              } } *> consumeOutput

          }

        ???
      }

  private def dockerCommand: Seq[String] =
    sys.env.get("HELIUM_SUDO_COMMAND").toList :+
      sys.env.getOrElse("HELIUM_DOCKER_COMMAND", "docker")


}
