package dev.helium_build.record

import java.io.File
import java.nio.charset.StandardCharsets

import cats.effect.Resource
import dev.helium_build.build.BuildSchema
import dev.helium_build.sdk.SdkInstallManager
import io.circe.Json
import org.apache.commons.io.FileUtils
import zio.{RefM, ZIO, ZManaged}
import zio.blocking.Blocking

final class NullRecorder[R <: Blocking] private
(
  metadataCache: RefM[Map[String, Json]],
  cacheDir: File,
  sdkDir: File,
  schemaFile: File,
  workDir: File,
) extends LiveRecorder[R](sdkDir = sdkDir, workDir = workDir) {

  override def schema: ZIO[R, Throwable, BuildSchema] =
    ZIO.accessM[Blocking] { _.blocking.effectBlocking {
      FileUtils.readFileToString(schemaFile, StandardCharsets.UTF_8)
    } }
      .flatMap(BuildSchema.parse)

  override def sdkInstallManager: ZIO[R, Throwable, SdkInstallManager] =
    SdkInstallManager(cacheDir)

  override def recordTransientMetadata(path: String)(fetch: ZIO[R, Throwable, Json]): ZIO[R, Throwable, Json] =
    metadataCache.modify { metadata =>
      fetch.map { data =>
        (data, metadata + (path -> data))
      }
    }

  override def recordArtifact(path: String)(fetch: ZIO[R, Throwable, File]): ZIO[R, Throwable, File] =
    fetch
}

object NullRecorder {

  def apply[R <: Blocking]
  (
    cacheDir: File,
    sdkDir: File,
    schemaFile: File,
    workDir: File,
  ): ZManaged[R, Throwable, NullRecorder[R]] =
    ZManaged.fromEffect(
      for {
        metadataCache <- RefM.make(Map.empty[String, Json])
      } yield new NullRecorder[R](metadataCache, cacheDir, sdkDir, schemaFile, workDir)
    )

}
