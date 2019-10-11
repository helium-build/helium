package dev.helium_build.proxy

import java.io.{File, RandomAccessFile}
import java.nio.file.{Files, StandardCopyOption}

import cats.effect.{Async, Sync}
import cats.effect.concurrent.Semaphore
import cats.implicits._

import org.apache.commons.io.FilenameUtils

object Cache {

  def cacheDownload[F[_]: Sync](cache: File, outFile: File, lock: Semaphore[F])(download: File => F[Unit]): F[File] =
    Sync[F].delay { cache.mkdirs() }.flatMap { _ =>
      lockSemaphore(lock)(
        lockFile(new File(cache, "cache.lock"))(
          Sync[F].delay { outFile.exists() }.flatMap {
            case true => outFile.pure[F]
            case false =>
              Sync[F].delay { File.createTempFile("download-", FilenameUtils.getExtension(outFile.getName), cache) }
                .flatMap { tempFile =>
                  (
                    for {
                      _ <- download(tempFile)

                      _ <- Sync[F].delay {
                        outFile.getParentFile.mkdirs()
                        Files.move(tempFile.toPath, outFile.toPath, StandardCopyOption.ATOMIC_MOVE)
                      }
                    } yield outFile
                  )
                    .recoverWith { case e =>
                      Sync[F].delay { Files.deleteIfExists(tempFile.toPath) } *>
                        Sync[F].raiseError(e)
                    }
                }
          }
        )
      )
    }


  private def lockFile[F[_]: Sync, A](file: File)(use: F[A]): F[A] =
    Sync[F].bracket(
      Sync[F].delay { new RandomAccessFile(file, "rw").getChannel }
    )(channel =>
      Sync[F].bracket(
        Sync[F].delay { channel.lock() }
      )(_ => use)(
        lock => Sync[F].delay { lock.release() }
      )
    )(
      channel => Sync[F].delay { channel.close() }
    )

  private def lockSemaphore[F[_]: Sync, A](lock: Semaphore[F])(use: F[A]): F[A] =
    Sync[F].bracket(
      lock.acquire
    )(_ => use)(
      _ => lock.release
    )

}
