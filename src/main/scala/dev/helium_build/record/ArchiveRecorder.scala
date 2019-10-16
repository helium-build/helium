package dev.helium_build.record

import java.io.{File, FileInputStream, FileOutputStream}
import java.nio.charset.StandardCharsets
import java.nio.file.Files

import cats.effect.Resource
import dev.helium_build.sdk.{SdkInfo, SdkInstallManager, SdkLoader}
import io.circe.{Json, JsonObject}
import org.apache.commons.compress.archivers.tar.{TarArchiveEntry, TarArchiveOutputStream, TarConstants}
import org.apache.commons.io.{FileUtils, IOUtils}
import zio.blocking.Blocking
import zio.{IO, Managed, RIO, Ref, Semaphore, Task, ZIO, ZManaged}
import zio.interop.catz._
import cats.implicits._
import dev.helium_build.build.BuildSchema
import dev.helium_build.conf.RepoConfig
import dev.helium_build.util.ArchiveUtil

final class ArchiveRecorder[R <: Blocking] private
(
  lock: Semaphore,
  recordedSchema: Ref[Option[String]],
  recordedRepoConfig: Ref[Option[String]],
  recordedSdks: Ref[Set[String]],
  recordedArtifacts: Ref[Set[String]],
  recordedMetadata: Ref[Map[String, Json]],
  archive: TarArchiveOutputStream,
  cacheDir: File,
  protected override val sdkDir: File,
  protected override val schemaFile: File,
  override val workDir: File,
  protected override val confDir: File,
) extends LiveRecorder[R] {
  import ArchiveRecorder._

  private def writeTextFile(path: String, content: String): RIO[R, Unit] =
    ZIO.accessM[R] { _.blocking.effectBlocking {
      val entry = new TarArchiveEntry(path)
      val bytes = content.getBytes(StandardCharsets.UTF_8)
      entry.setSize(bytes.length)
      archive.putArchiveEntry(entry)

      IOUtils.write(bytes, archive)
      archive.closeArchiveEntry()
    } }

  override protected def cacheBuildSchema(readBuildSchema: RIO[R, String]): RIO[R, String] =
    lock.withPermit(
      recordedSchema.get.flatMap {
        case Some(schema) => IO.succeed(schema)
        case None =>
          for {
            data <- readBuildSchema
            _ <- recordedSchema.set(Some(data))
            _ <- writeTextFile(buildSchemaPath, data)
          } yield data
      }
    )

  override def sdkInstallManager: ZIO[R, Throwable, SdkInstallManager] = for {
    sdkInstallManager <- SdkInstallManager(cacheDir)
  } yield new SdkInstallManager {
    override def getInstalledSdkDir(sdk: SdkInfo): RIO[Blocking, (String, File)] = lock.withPermit {
      val sdkHash = SdkLoader.sdkSha256(sdk)
      recordedSdks.get.flatMap { sdks =>
        if(sdks.contains(sdkHash)) {
          sdkInstallManager.getInstalledSdkDir(sdk)
        } else {
          for {
            (hash, dir) <- sdkInstallManager.getInstalledSdkDir(sdk)
            _ <- ArchiveUtil.addDirToArchive(archive, sdkPath(hash), dir.getParentFile)
            _ <- recordedSdks.set(sdks + hash)
          } yield (hash, dir)
        }
      }
    }
  }


  override protected def cacheRepoConfig(readRepoConfig: RIO[R, String]): RIO[R, String] =
    lock.withPermit(
      recordedRepoConfig.get.flatMap {
        case Some(config) => IO.succeed(config)
        case None =>
          for {
            data <- readRepoConfig
            _ <- recordedRepoConfig.set(Some(data))
            _ <- writeTextFile(repoConfigPath, data)
          } yield data
      }
    )

  override def recordTransientMetadata(path: String)(fetch: ZIO[R, Throwable, Json]): ZIO[R, Throwable, Json] = {
    lock.withPermit(
      recordedMetadata.get.flatMap { metadata =>
        metadata.get(path) match {
          case Some(value) =>
            IO.succeed(value)
          case None =>
            for {
              value <- fetch
              _ <- recordedMetadata.set(metadata + (path -> value))
            } yield value
        }
      }
    )
  }


  def recordArtifact(path: String)(fetch: File => ZIO[R, Throwable, File]): ZIO[R, Throwable, File] =
    lock.withPermit(
      recordedArtifacts.get.flatMap { artifacts =>
        if(artifacts.contains(path))
          fetch(cacheDir)
        else
          for {
            file <- fetch(cacheDir)
            _ <- ArchiveUtil.addFileToArchive(archive, artifactPath(path), file)
            _ <- recordedArtifacts.set(artifacts + path)
          } yield file
      }
    )


  private def writeMetadata: ZIO[R, Throwable, Unit] =
    recordedMetadata.get.flatMap { metadata =>
      ZIO.accessM { _.blocking.effectBlocking {
        val entry = new TarArchiveEntry(transientMetadataPath)
        val data = Json.fromJsonObject(JsonObject.fromMap(metadata)).noSpaces.getBytes(StandardCharsets.UTF_8)
        entry.setSize(data.length)
        archive.putArchiveEntry(entry)
        IOUtils.write(data, archive)
        archive.closeArchiveEntry()
      } }
    }


}

object ArchiveRecorder {

  def apply[R <: Blocking]
  (
    cacheDir: File,
    sdkDir: File,
    schemaFile: File,
    archiveFile: File,
    workDir: File,
    confDir: File,
  ): ZManaged[R, Throwable, ArchiveRecorder[R]] =
    for {
      outStream <- ZManaged.fromAutoCloseable(ZIO.accessM[R] { _.blocking.effectBlocking { new FileOutputStream(archiveFile) }})
      tarStream <- ZManaged.fromAutoCloseable(ZIO.accessM[R] { _.blocking.effectBlocking { new TarArchiveOutputStream(outStream) }})
      _ <- ZManaged.fromEffect(IO.effect { tarStream.setLongFileMode(TarArchiveOutputStream.LONGFILE_POSIX) })
      _ <- ZManaged.fromEffect(ArchiveUtil.addDirToArchive(tarStream, workDirPath, workDir))
      recorder <- ZManaged.make[R, Throwable, ArchiveRecorder[R]](
        for {
          lock <- Semaphore.make(1)
          recordedSchema <- Ref.make(Option.empty[String])
          recordedRepoConfig <- Ref.make(Option.empty[String])
          recordedSdks <- Ref.make(Set.empty[String])
          recordedArtifacts <- Ref.make(Set.empty[String])
          recordedMetadata <- Ref.make(Map.empty[String, Json])
        } yield new ArchiveRecorder[R](
          lock,
          recordedSchema,
          recordedRepoConfig,
          recordedSdks,
          recordedArtifacts,
          recordedMetadata,
          tarStream,
          cacheDir = cacheDir,
          sdkDir = sdkDir,
          schemaFile = schemaFile,
          workDir = workDir,
          confDir = confDir,
        )
      )(_.writeMetadata.orDie)
    } yield recorder


  val buildSchemaPath = "build.toml"
  val repoConfigPath = "conf/repos.toml"
  val transientMetadataPath = "dependencies-metadata.json"
  val workDirPath = "work"
  def sdkPath(hash: String) = "sdks/" + hash
  def artifactPath(path: String) = "dependencies/" + path


}

