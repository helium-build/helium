package dev.helium_build.docker

import java.io.InputStreamReader
import java.nio.charset.StandardCharsets

import org.apache.commons.io.IOUtils
import zio._
import zio.blocking.Blocking

trait NetworkHandler {
  def createNetwork: RIO[Blocking, String]
  def destroyNetworks(ids: String*): RIO[Blocking, Unit]
  def cleanupNetworks: RIO[Blocking, Unit]
}

object NetworkHandler {

  private val label = "helium=dev-env"

  import ProcessHelpers._

  def apply(): UIO[NetworkHandler] = for {
    pid <- IO.effectTotal { ProcessHandle.current().pid() }
    index <- Ref.make(0)
  } yield new NetworkHandler {
    override def createNetwork: RIO[Blocking, String] = for {
      currIndex <- index.update { _ + 1 }
      id <- runCommandOutput("docker", "network", "create", "--label", label, s"helium-dev-env-$pid-$currIndex")
    } yield id.trim

    override def destroyNetworks(ids: String*): RIO[Blocking, Unit] =
      runCommandOutput(Seq("docker", "network", "rm") ++ ids: _*).unit

    override def cleanupNetworks: RIO[Blocking, Unit] =
      runCommandOutput("docker", "network", "ls", "--filter", s"label=$label", "--no-trunc", "--format", "{{.ID}}")
        .flatMap { foundNetworks =>
          destroyNetworks(foundNetworks.split("\\s").filterNot { _.isEmpty }.toSeq: _*)
        }
  }

}
