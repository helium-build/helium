package dev.helium_build.docker

import java.io.File

final case class LaunchProperties
(
  dockerImage: String,
  command: Seq[String],
  env: Map[String, String],
  pathDirs: Seq[String],
  sdkDirs: Seq[(String, File)],
  sourcesDir: File,
  installDir: File,
  socketDir: File,
)
