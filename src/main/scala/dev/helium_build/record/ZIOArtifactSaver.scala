package dev.helium_build.record

import zio._
import cats.implicits._

trait ZIOArtifactSaver[R] extends ArtifactSaver[ZIO[R, Throwable, *]] {

}
