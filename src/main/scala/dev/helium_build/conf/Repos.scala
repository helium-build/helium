package dev.helium_build.conf

final case class Repos
(
  maven: Option[List[MavenRepo]],
  nuget: Option[List[NuGetRepo]],
  npm: Option[NpmRepo],
)
