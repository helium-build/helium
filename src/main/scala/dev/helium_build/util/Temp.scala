package dev.helium_build.util

import java.io.File
import java.nio.file.{Files, Path}

import org.apache.commons.io.FileUtils
import zio.blocking.Blocking
import zio.{ZIO, ZManaged}

object Temp {

  def createTempPath[R <: Blocking, E](create: ZIO[R, E, Path]): ZManaged[R, E, Path] =
    ZManaged.make(create) { path =>
      ZIO.accessM[Blocking] { _.blocking.effectBlocking {
        if(Files.exists(path)) {
          if(Files.isDirectory(path)) {
            FileUtils.deleteDirectory(path.toFile)
          }
          else {
            Files.deleteIfExists(path)
          }
        }
      }.orDie }
    }

  def createTemp[R <: Blocking, E](create: ZIO[R, E, File]): ZManaged[R, E, File] =
    ZManaged.make(create) { file =>
      ZIO.accessM[Blocking] { _.blocking.effectBlocking {
        if(file.exists()) {
          if(file.isDirectory) {
            FileUtils.deleteDirectory(file)
          }
          else {
            file.delete()
          }
        }
      }.orDie }
    }

}
