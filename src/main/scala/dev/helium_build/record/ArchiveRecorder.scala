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
import zio.{IO, Managed, Ref, Semaphore, Task, RIO, ZIO, ZManaged}
import zio.interop.catz._
import cats.implicits._
import dev.helium_build.build.BuildSchema
import dev.helium_build.util.ArchiveUtil

final class ArchiveRecorder[R <: Blocking] private
(
  lock: Semaphore,
  recordedSchema: Ref[Option[BuildSchema]],
  recordedSdks: Ref[Set[String]],
  recordedArtifacts: Ref[Set[String]],
  recordedMetadata: Ref[Map[String, Json]],
  archive: TarArchiveOutputStream,
  cacheDir: File,
  sdkDir: File,
  schemaFile: File,
  workDir: File,
) extends LiveRecorder[R](sdkDir = sdkDir, workDir = workDir) {


  override def schema: ZIO[R, Throwable, BuildSchema] =
    lock.withPermit(
      recordedSchema.get.flatMap {
        case Some(schema) => IO.succeed(schema)
        case None =>
          for {
            data <- ZIO.accessM[Blocking] { _.blocking.effectBlocking {
              FileUtils.readFileToString(schemaFile, StandardCharsets.UTF_8)
            } }
            schema <- BuildSchema.parse(data)
            _ <- recordedSchema.set(Some(schema))
            _ <- ZIO.accessM[R] { _.blocking.effectBlocking {
              val entry = new TarArchiveEntry("build.toml")
              val bytes = data.getBytes(StandardCharsets.UTF_8)
              entry.setSize(bytes.length)
              archive.putArchiveEntry(entry)

              IOUtils.write(bytes, archive)
              archive.closeArchiveEntry()
            } }
          } yield schema
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
            _ <- ArchiveUtil.addDirToArchive(archive, "sdks/" + hash, dir.getParentFile)
            _ <- recordedSdks.set(sdks + hash)
          } yield (hash, dir)
        }
      }
    }
  }

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


  def recordArtifact(path: String)(fetch: ZIO[R, Throwable, File]): ZIO[R, Throwable, File] =
    lock.withPermit(
      recordedArtifacts.get.flatMap { artifacts =>
        if(artifacts.contains(path))
          fetch
        else
          for {
            file <- fetch
            _ <- ArchiveUtil.addFileToArchive(archive, "dependencies/" + path, file)
            _ <- recordedArtifacts.set(artifacts + path)
          } yield file
      }
    )


  private def writeMetadata: ZIO[R, Throwable, Unit] =
    recordedMetadata.get.flatMap { metadata =>
      ZIO.accessM { _.blocking.effectBlocking {
        val entry = new TarArchiveEntry("dependencies-metadata.json")
        val data = Json.fromJsonObject(JsonObject.fromMap(metadata)).noSpaces.getBytes(StandardCharsets.UTF_8)
        entry.setSize(data.length)
        archive.putArchiveEntry(entry)
        IOUtils.write(data, archive)
        archive.closeArchiveEntry()
      } }
    }


}

object ArchiveRecorder {

  def apply[R <: Blocking](cacheDir: File, sdkDir: File, schemaFile: File, archiveFile: File, workDir: File): ZManaged[R, Throwable, ArchiveRecorder[R]] =
    for {
      outStream <- ZManaged.fromAutoCloseable(ZIO.accessM[R] { _.blocking.effectBlocking { new FileOutputStream(archiveFile) }})
      tarStream <- ZManaged.fromAutoCloseable(ZIO.accessM[R] { _.blocking.effectBlocking { new TarArchiveOutputStream(outStream) }})
      _ <- ZManaged.fromEffect(IO.effect { tarStream.setLongFileMode(TarArchiveOutputStream.LONGFILE_POSIX) })
      _ <- ZManaged.fromEffect(ArchiveUtil.addDirToArchive(tarStream, "work", workDir))
      recorder <- ZManaged.make[R, Throwable, ArchiveRecorder[R]](
        for {
          lock <- Semaphore.make(1)
          recordedSchema <- Ref.make(Option.empty[BuildSchema])
          recordedSdks <- Ref.make(Set.empty[String])
          recordedArtifacts <- Ref.make(Set.empty[String])
          recordedMetadata <- Ref.make(Map.empty[String, Json])
        } yield new ArchiveRecorder[R](
          lock,
          recordedSchema,
          recordedSdks,
          recordedArtifacts,
          recordedMetadata,
          tarStream,
          cacheDir = cacheDir,
          sdkDir = sdkDir,
          schemaFile = schemaFile,
          workDir = workDir,
        )
      )(_.writeMetadata.orDie)
    } yield recorder

}

