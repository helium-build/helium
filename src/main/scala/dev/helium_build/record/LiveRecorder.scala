package dev.helium_build.record
import java.io.File
import java.nio.charset.StandardCharsets
import java.nio.file.Files

import cats.implicits._
import dev.helium_build.build.BuildSchema
import dev.helium_build.conf.RepoConfig
import dev.helium_build.sdk.{SdkInfo, SdkInstallManager, SdkLoader}
import org.apache.commons.io.FileUtils
import zio._
import zio.blocking.Blocking
import zio.stream.ZStream

abstract class LiveRecorder[R <: Blocking] extends ZIORecorder[R]  {

  protected val schemaFile: File
  protected val sdkDir: File
  protected val confDir: File

  protected def cacheBuildSchema(readBuildSchema: RIO[R, String]): RIO[R, String] = readBuildSchema
  protected def cacheRepoConfig(readRepoConfig: RIO[R, String]): RIO[R, String] = readRepoConfig

  final override def schema: ZIO[R, Throwable, BuildSchema] =
    cacheBuildSchema(ZIO.accessM[Blocking] { _.blocking.effectBlocking {
      FileUtils.readFileToString(schemaFile, StandardCharsets.UTF_8)
    } })
      .flatMap(BuildSchema.parse)

  final override def availableSdks: ZStream[R, Throwable, SdkInfo] =
    SdkLoader.loadSdks(sdkDir)


  override def repoConfig: ZIO[R, Throwable, RepoConfig] =
    cacheRepoConfig(ZIO.accessM[Blocking] { _.blocking.effectBlocking { Files.readString(new File(confDir, "repos.toml").toPath) } })
      .flatMap { confData =>
        IO.fromEither(RepoConfig.parse(confData).leftMap { new RuntimeException(_) })
      }

}
