package dev.helium_build.record

import java.io.File

import cats.effect.Resource
import dev.helium_build.build.BuildSchema
import dev.helium_build.conf.RepoConfig
import dev.helium_build.sdk.SdkInstallManager
import io.circe.Json
import zio.{ZIO, ZManaged}
import zio.blocking.Blocking

trait Recorder[F[_]] {

  def schema: F[BuildSchema]
  def sourcesDir: File
  def repoConfig: F[RepoConfig]

  def recordTransientMetadata(path: String)(fetch: F[Json]): F[Json]
  def recordArtifact(path: String)(fetch: File => F[File]): F[File]

}
