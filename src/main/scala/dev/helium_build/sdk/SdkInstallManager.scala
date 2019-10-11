package dev.helium_build.sdk

import java.io.{BufferedInputStream, File, FileInputStream, FileOutputStream}
import java.nio.charset.StandardCharsets
import java.nio.file.{Files, Path}
import java.nio.file.attribute.PosixFilePermission
import java.util

import zio._
import zio.blocking.Blocking
import zio.interop.catz._
import cats._
import cats.implicits._
import dev.helium_build.util.ArchiveUtil
import org.apache.commons.io.{FileUtils, FilenameUtils}
import com.softwaremill.sttp._
import com.softwaremill.sttp.circe._
import com.softwaremill.sttp.asynchttpclient.zio._
import org.apache.commons.codec.binary.Hex
import org.apache.commons.codec.digest.DigestUtils
import org.apache.commons.compress.archivers.tar.{TarArchiveEntry, TarArchiveInputStream}
import org.apache.commons.compress.archivers.zip.ZipArchiveInputStream
import org.apache.commons.compress.compressors.gzip.GzipCompressorInputStream
import zio.stream.ZStream

trait SdkInstallManager {

  def getInstalledSdkDir(sdk: SdkInfo): RIO[Blocking, (String, File)]

}

object SdkInstallManager {

  private implicit val sttpBackend = AsyncHttpClientZioBackend()

  def apply(cacheDir: File): ZIO[Blocking, Nothing, SdkInstallManager] = for {
    baseDir <- ZIO.accessM[Blocking] { _.blocking.effectBlocking { new File(cacheDir, "sdks").getAbsoluteFile }.orDie }
    sdkStore <- RefM.make(Map[String, Task[(String, File)]]())
  } yield new SdkInstallManager {
    override def getInstalledSdkDir(sdk: SdkInfo): RIO[Blocking, (String, File)] =
      sdkStore.modify { sdks =>
        val sdkHash = SdkLoader.sdkSha256(sdk)
        sdks.get(sdkHash) match {
          case Some(value) => IO.succeed((value, sdks))
          case None =>
            for {
              fiber <- {
                for {
                  dir <- ZIO.accessM[Blocking] { _.blocking.effectBlocking { new File(baseDir, sdkHash) } }
                  _ <- ZIO.accessM[Blocking] { _.blocking.effectBlocking { dir.mkdirs() } }
                  installDir <- installSdk(dir, sdk)
                } yield (sdkHash, installDir)
              }.fork

              task = fiber.join
            } yield (task, sdks.updated(sdkHash, task))
        }
      }.flatten

    private def installSdk(dir: File, sdk: SdkInfo): RIO[Blocking, File] =
      ZIO.accessM[Blocking] { _.blocking.effectBlocking { new File(dir, "sdk.json") } }.flatMap { sdkFile =>
        IO.effect { new File(dir, "install") }.flatMap { installDir =>
          ZIO.accessM[Blocking] { _.blocking.effectBlocking { sdkFile.exists() } }.flatMap {
            case true => IO.succeed(installDir)
            case false =>
              for {
                _ <- ZIO.accessM[Blocking] { _.blocking.effectBlocking {
                  installDir.mkdirs()
                  FileUtils.cleanDirectory(installDir)
                } }
                _ <- evalInstallSteps(installDir, sdk)
                _ <- SdkLoader.saveSdk(sdk, sdkFile)
              } yield installDir
          }
        }
      }

    private def evalInstallSteps(installDir: File, sdk: SdkInfo): RIO[Blocking, Unit] =
      sdk.setupSteps.toVector.traverse_ {
        case SdkDownload(url, fileName, hash) =>
          for {
            nFileName <- ArchiveUtil.normalizePath(fileName)
            outFile <- ZIO.accessM[Blocking] { _.blocking.effectBlocking { new File(installDir, nFileName) } }
            response <- sttp.get(Uri(java.net.URI.create(url))).response(asFile(outFile)).send()
            _ <- if(response.isSuccess) IO.succeed(()) else IO.fail(new RuntimeException(response.toString()))
            _ <- validateHash(outFile, hash)
          } yield ()

        case SdkExtract(fileName, directory) =>
            for {
              nFileName <- ArchiveUtil.normalizePath(fileName)
              nFile <- IO.effect { new File(installDir, nFileName) }
              nDirName <- ArchiveUtil.normalizePath(directory)
              nDir <- IO.effect { new File(installDir, nDirName) }
              _ <- ArchiveUtil.extractArchive(nFile, nDir)
            } yield ()

        case SdkDelete(fileName) =>
          for {
            nFileName <- ArchiveUtil.normalizePath(fileName)
            _ <- ZIO.accessM[Blocking] { _.blocking.effectBlocking { new File(installDir, nFileName).delete() } }
          } yield ()

        case SdkCreateDirectory(fileName) =>
          for {
            nFileName <- ArchiveUtil.normalizePath(fileName)
            _ <- ZIO.accessM[Blocking] { _.blocking.effectBlocking {
              new File(installDir, nFileName).mkdirs()
            } }
          } yield ()

        case SdkCreateFile(fileName, isExecutable, content) =>
          for {
            nFileName <- ArchiveUtil.normalizePath(fileName)
            _ <- ZIO.accessM[Blocking] { _.blocking.effectBlocking {
              val outFile = new File(installDir, nFileName).toPath
              Files.writeString(outFile, content, StandardCharsets.UTF_8)
              if(isExecutable) {
                try {
                  val perms = Files.getPosixFilePermissions(outFile)
                  perms.add(PosixFilePermission.OWNER_EXECUTE)
                  Files.setPosixFilePermissions(outFile, perms)
                }
                catch {
                  case _: UnsupportedOperationException => ()
                }
              }
            } }
          } yield ()

      }

    private def validateHash(file: File, hash: SdkHash): RIO[Blocking, Unit] =
    {
      val (digest, expected) = hash match {
        case SdkHash.Sha256(value) => (DigestUtils.getSha256Digest, value)
        case SdkHash.Sha512(value) => (DigestUtils.getSha512Digest, value)
      }

      ZIO.accessM[Blocking] { _.blocking.effectBlocking { DigestUtils.digest(digest, file) } }
        .map(Hex.encodeHexString)
        .flatMap { actual =>
          if(expected.equalsIgnoreCase(actual))
            IO.succeed(())
          else
            IO.fail(new RuntimeException(s"Invalid hash for file ${file.toString}"))
        }
    }

  }

}
