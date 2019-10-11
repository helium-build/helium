package dev.helium_build.conf

final case class MavenRepo
(
  name: String,
  url: String,
) {

  def createMap: Map[String, Any] = Map(
    "name" -> name,
    "url" -> s"http://localhost:9000/maven/$name/",
  )

}
