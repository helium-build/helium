package dev.helium_build.conf

final case class NuGetRepo
(
  name: String,
  url: String,
) {

  def createMap: Map[String, Any] = Map(
    "name" -> name,
    "url" -> s"http://localhost:9000/nuget/$name/v3/index.json",
  )

}
