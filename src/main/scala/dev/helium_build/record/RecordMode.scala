package dev.helium_build.record

import java.io.File

sealed trait RecordMode

object RecordMode {
  final case class Null(workDir: File, buildSchema: File, cacheDir: File, sdkDir: File) extends RecordMode
  final case class Archive(workDir: File, buildSchema: File, archive: File, cacheDir: File, sdkDir: File) extends RecordMode
  final case class Replay(archive: File) extends RecordMode
}
