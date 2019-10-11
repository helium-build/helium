package dev.helium_build.record

import java.io.File

import cats.effect.Resource
import dev.helium_build.build.BuildSchema
import dev.helium_build.sdk.SdkInstallManager
import io.circe.Json
import zio.{ZIO, ZManaged}
import zio.blocking.Blocking

trait Recorder[F[_]] {

  def schema: F[BuildSchema]
  def workDir: File

  def recordTransientMetadata(path: String)(fetch: F[Json]): F[Json]
  def recordArtifact(path: String)(fetch: F[File]): F[File]

}

object Recorder {

  def from[R <: Blocking](cacheDir: File, sdkDir: File, mode: RecordMode): ZManaged[R, Throwable, ZIORecorder[R]] =
    mode match {
      case RecordMode.Null(workDir, buildSchema) =>
        NullRecorder(
          cacheDir = cacheDir,
          sdkDir = sdkDir,
          schemaFile = buildSchema,
          workDir = workDir,
        )

      case RecordMode.Archive(workDir, buildSchema, archive) =>
        ArchiveRecorder(
          cacheDir = cacheDir,
          sdkDir = sdkDir,
          schemaFile = buildSchema,
          archiveFile = archive,
          workDir = workDir,
        )

      case RecordMode.Replay(archive) =>
        ReplayRecorder(archive)
    }

}
