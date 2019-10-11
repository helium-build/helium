package dev.helium_build.record

import java.io.File

trait ArtifactSaver[F[_]] {
  def saveArtifact(name: String, stream: fs2.Stream[F, Byte]): F[Unit]
  def saveArtifact(stream: fs2.Stream[F, Byte])(nameSelector: File => F[String]): F[Unit]
}
