package dev.helium_build.docker

import java.nio.charset.StandardCharsets

import org.apache.commons.io.IOUtils
import zio._
import zio.blocking.Blocking

private[docker] object ProcessHelpers {

  def waitForExit(proc: Process): Task[Unit] =
    ZIO.effectAsync { register =>
      proc.onExit().whenComplete { (_, ex) =>
        register(
          if(ex == null)
            IO.succeed(())
          else
            IO.fail(ex)
        )
      }
    }

  def runCommandOutput(args: String*): RIO[Blocking, String] =
    ZIO.accessM[Blocking] { _.blocking.effectBlocking {
      val proc = new ProcessBuilder(args: _*)
        .redirectOutput(ProcessBuilder.Redirect.PIPE)
        .start()

      val output = IOUtils.toString(proc.getInputStream, StandardCharsets.UTF_8)

      (proc, output)
    } }
      .flatMap {
        case (proc, output) =>
          waitForExit(proc).flatMap { _ =>
            IO.effect { proc.exitValue() }.flatMap {
              case 0 => IO.succeed(output)
              case x => IO.fail(new RuntimeException(s"Process failed with exit code $x"))
            }
          }
      }

}
