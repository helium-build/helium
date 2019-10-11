package dev.helium_build.conf

import toml.Toml
import toml.Codecs._

final case class Config
(
  repo: Option[Repos]
) {

  def createMap: Map[String, Any] = Map(
    "repo_maven" -> repo.flatMap { _.maven }.getOrElse(Nil).map(_.createMap),
    "repo_nuget" -> repo.flatMap { _.nuget }.getOrElse(Nil).map(_.createMap),
    "repo_npm_registry" -> repo.flatMap { _.npm }.map { _.registry }.orNull,
    "nuget_push_url" -> "http://localhost:9000/nuget/publish",
  )

}

object Config {

  def parse(data: String): Either[String, Config] =
    Toml.parseAs[Config](data).left.map { case (addr, msg) => addr.toString + " " + msg }

}
