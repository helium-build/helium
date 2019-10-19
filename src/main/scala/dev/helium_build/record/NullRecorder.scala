package dev.helium_build.record

import java.io.File
import java.nio.charset.StandardCharsets
import java.nio.file.Files

import cats.effect.Resource
import cats.implicits._
import dev.helium_build.build.BuildSchema
import dev.helium_build.conf.RepoConfig
import dev.helium_build.sdk.SdkInstallManager
import io.circe.Json
import org.apache.commons.io.FileUtils
import zio.{IO, RefM, ZIO, ZManaged}
import zio.blocking.Blocking

final class NullRecorder[R <: Blocking] private
(
  metadataCache: RefM[Map[String, Json]],

  cacheDir: File,
  protected override val sdkDir: File,
  protected override val schemaFile: File,
  override val sourcesDir: File,
  protected override val confDir: File,
) extends LiveRecorder[R] {

  override def sdkInstallManager: ZIO[R, Throwable, SdkInstallManager] =
    SdkInstallManager(cacheDir)

  override def recordTransientMetadata(path: String)(fetch: ZIO[R, Throwable, Json]): ZIO[R, Throwable, Json] =
    metadataCache.modify { metadata =>
      fetch.map { data =>
        (data, metadata + (path -> data))
      }
    }

  override def recordArtifact(path: String)(fetch: File => ZIO[R, Throwable, File]): ZIO[R, Throwable, File] =
    fetch(cacheDir)
}

object NullRecorder {

  def apply[R <: Blocking]
  (
    cacheDir: File,
    sdkDir: File,
    schemaFile: File,
    sourcesDir: File,
    confDir: File,
  ): ZManaged[R, Throwable, NullRecorder[R]] =
    ZManaged.fromEffect(
      for {
        metadataCache <- RefM.make(Map.empty[String, Json])
      } yield new NullRecorder[R](
        metadataCache,
        cacheDir = cacheDir,
        sdkDir = sdkDir,
        schemaFile = schemaFile,
        sourcesDir = sourcesDir,
        confDir = confDir,
      )
    )

}
