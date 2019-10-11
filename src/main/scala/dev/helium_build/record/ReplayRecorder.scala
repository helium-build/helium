package dev.helium_build.record

import java.io.File
import java.nio.charset.StandardCharsets
import java.nio.file.Files

import dev.helium_build.build.BuildSchema
import dev.helium_build.sdk.{SdkInfo, SdkInstallManager, SdkLoader}
import dev.helium_build.util.{ArchiveUtil, Temp}
import io.circe.{Json, JsonObject}
import org.apache.commons.io.FileUtils
import zio.{IO, RIO, ZIO, ZManaged}
import zio.blocking.Blocking
import zio.stream._

final class ReplayRecorder[R <: Blocking] private
(
  extractedDir: File,
  dependencyMetadata: Map[String, Json]
)  extends ZIORecorder[R] {

  override def schema: ZIO[R, Throwable, BuildSchema] =
    ZIO.accessM[Blocking] { _.blocking.effectBlocking {
      FileUtils.readFileToString(new File(extractedDir, "build.toml"), StandardCharsets.UTF_8)
    } }
      .flatMap(BuildSchema.parse)

  override def workDir: File = new File(extractedDir, "work")


  override def sdkInstallManager: ZIO[R, Throwable, SdkInstallManager] = IO.succeed(
    new SdkInstallManager {
      override def getInstalledSdkDir(sdk: SdkInfo): RIO[Blocking, (String, File)] = {
        val sdkHash = SdkLoader.sdkSha256(sdk)
        val sdkDir = new File(new File(extractedDir, "sdks"), sdkHash)

        ZIO.accessM[Blocking] { _.blocking.effectBlocking {
          sdkDir.exists()
        } }
          .flatMap {
            case true => IO.succeed(sdkHash -> new File(sdkDir, "install"))
            case false => IO.fail(new RuntimeException("Unknown sdk"))
          }
      }
    }
  )


  override def availableSdks: ZStream[R, Throwable, SdkInfo] =
    ZStream.fromEffect(
      ZIO.accessM[Blocking] { _.blocking.effectBlocking {
        new File(extractedDir, "sdks").listFiles().toSeq
      } }
    )
      .flatMap(Stream.fromIterable)
      .mapM { sdkDir =>
        val sdkInfoFile = new File(sdkDir, "sdk.json")
        SdkLoader.loadSdk(sdkInfoFile)
      }


  override def recordArtifact(path: String)(fetch: ZIO[R, Throwable, File]): ZIO[R, Throwable, File] = {
    val file = new File(new File(extractedDir, "dependencies"), path)
    ZIO.accessM[Blocking] { _.blocking.effectBlocking {
      file.exists()
    } }.flatMap {
      case true => IO.succeed(file)
      case false => IO.fail(new RuntimeException(s"Unknown artifact path: $path"))
    }
  }

  override def recordTransientMetadata(path: String)(fetch: ZIO[R, Throwable, Json]): ZIO[R, Throwable, Json] =
    dependencyMetadata.get(path) match {
      case Some(value) => IO.succeed(value)
      case None => IO.fail(new RuntimeException(s"Unknown metadata path: $path"))
    }
}

object ReplayRecorder {


  def apply[R <: Blocking](archiveFile: File): ZManaged[R, Throwable, ReplayRecorder[R]] =
    for {
      extractDir <- Temp.createTempPath(
        ZIO.accessM[Blocking] { _.blocking.effectBlocking { Files.createTempDirectory("helium-replay-") } }
      )

      _ <- ZManaged.fromEffect(ArchiveUtil.extractArchive(archiveFile, extractDir.toFile))

      metadata <- ZManaged.fromEffect(
        ZIO.accessM[Blocking] { _.blocking.effectBlocking {
          FileUtils.readFileToString(new File(extractDir.toFile, "dependencies-metadata.json"), StandardCharsets.UTF_8)
        } }
          .flatMap { metadataStr =>
            IO.fromEither(io.circe.parser.decode[JsonObject](metadataStr))
          }
      )

    } yield new ReplayRecorder[R](extractDir.toFile, metadata.toMap)

}
