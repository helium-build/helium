package dev.helium_build.build

import toml.Toml
import toml.Codecs._
import zio._

final case class BuildSchema
(
  build: BuildInfo,
  sdk: List[RequiredSdk],
)

final case class BuildInfo
(
  command: List[String],
)

final case class RequiredSdk
(
  name: String,
  version: String,
)

object BuildSchema {

  def parse(body: String): Task[BuildSchema] =
    IO.fromEither(Toml.parseAs[BuildSchema](body))
      .mapError {
        case (_, message) => new RuntimeException(message)
      }

}
