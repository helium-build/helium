package dev.helium_build.record

import java.io.File
import java.nio.file.{FileAlreadyExistsException, Files, StandardOpenOption}

import cats.effect.Blocker
import dev.helium_build.util.Temp
import zio.{IO, Semaphore, ZIO, ZManaged}
import zio.blocking.Blocking
import zio.interop.catz._

final class FSArtifactSaver[R <: Blocking](outputDir: File) extends ZIOArtifactSaver[R] {
  override def saveArtifact(name: String, stream: fs2.Stream[ZIO[R, Throwable, *], Byte]): ZIO[R, Throwable, Unit] = for {
    _ <- ZIO.accessM[R] { _.blocking.effectBlocking { outputDir.mkdirs() } }

    executor <- ZIO.accessM[R] { _.blocking.blockingExecutor }
    blocker = Blocker.liftExecutionContext(executor.asEC)

    _ <- stream
      .through(fs2.io.file.writeAll[ZIO[R, Throwable, *]](new File(outputDir, name).toPath, blocker, Seq(StandardOpenOption.CREATE_NEW)))
      .compile
      .drain
      .catchSome {
        case _: FileAlreadyExistsException => IO.fail(new ArtifactAlreadyExistsException)
      }

  } yield ()

  override def saveArtifact(stream: fs2.Stream[ZIO[R, Throwable, *], Byte])(nameSelector: File => ZIO[R, Throwable, String]): ZIO[R, Throwable, Unit] =
    ZIO.accessM[R] { _.blocking.effectBlocking { outputDir.mkdirs() } }.flatMap { _ =>
      ZIO.accessM[R] { _.blocking.blockingExecutor }.flatMap { executor =>
        val blocker = Blocker.liftExecutionContext(executor.asEC)

        Temp.createTemp(ZIO.accessM[R] { _.blocking.effectBlocking { File.createTempFile("artifact-", null, outputDir) } }).use { tempFile =>
          for {
            _ <- stream
              .through(fs2.io.file.writeAll[ZIO[R, Throwable, *]](tempFile.toPath, blocker, Seq(StandardOpenOption.CREATE)))
              .compile
              .drain

            fileName <- nameSelector(tempFile)

            _ <- ZIO.accessM[R] { _.blocking.effectBlocking {
              try Files.move(tempFile.toPath, new File(outputDir, fileName).toPath)
              catch {
                case _: FileAlreadyExistsException => throw new ArtifactAlreadyExistsException
              }
            } }
          } yield ()
        }
      }
    }

}
