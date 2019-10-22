package dev.helium_build.proxy

import java.io.{File, IOException, RandomAccessFile}
import java.nio.file.{CopyOption, Files, StandardCopyOption}
import java.security.MessageDigest

import cats.effect.concurrent.Semaphore
import cats.effect.{Async, Blocker, ContextShift, Sync}
import cats.implicits._
import dev.helium_build.record.Recorder
import sttp.client.{SttpBackend, asByteArray, basicRequest}
import sttp.model.{Uri => sttpUri}
import sttp.client.asynchttpclient.cats._
import org.apache.commons.io.{FileUtils, FilenameUtils}
import org.http4s.{HttpRoutes, StaticFile, Uri}
import org.http4s.dsl.Http4sDsl
import org.http4s.dsl.impl.Path
import sttp.client.asynchttpclient.WebSocketHandler

import scala.jdk.CollectionConverters._

object MavenIvyRoutes {

  def routes[F[_]: Async : ContextShift](recorder: Recorder[F], blocker: Blocker, lock: Semaphore[F], mode: String, name: String, baseUrl: sttpUri)(implicit sttpBackend: SttpBackend[F, Nothing, WebSocketHandler]): HttpRoutes[F] = {
    val dsl = new Http4sDsl[F]{}
    import dsl._
    HttpRoutes.of[F] {
      case request @ GET -> `mode` /: `name` /: path if validatePath(path) =>
        recorder.recordArtifact(mode + "/" + name + "/" + path.toList.mkString("/")) { cacheDir =>
          resolveArtifact(blocker, mode, lock, cacheDir, name, baseUrl, path)
        }
          .flatMap { file =>
            StaticFile.fromFile(file, blocker, Some(request)).getOrElseF(NotFound())
          }
          .handleErrorWith {
            case _: IOException =>
              NotFound()

            case ex => Sync[F].raiseError(ex)
          }

      case HEAD -> `mode` /: `name` /: path if validatePath(path) =>
        recorder.recordArtifact(mode + "/" + name + "/" + path.toList.mkString("/")) { cacheDir =>
          resolveArtifact(blocker, mode, lock, cacheDir, name, baseUrl, path)
        }
          .flatMap { _ =>
            Ok()
          }
          .handleErrorWith {
            case _: IOException =>
              NotFound()

            case ex => Sync[F].raiseError(ex)
          }

    }
  }

  private def validatePath(path: Path): Boolean = {
    val parts = path.toList
    parts.nonEmpty && parts.forall { part => part.matches("[a-zA-Z0-9\\-_][a-zA-Z0-9\\-_\\.]*") }
  }

  private def resolveArtifact[F[_]: Sync : ContextShift](blocker: Blocker, mode: String, lock: Semaphore[F], cacheDir: File, name: String, baseUrl: sttpUri, path: Path)(implicit sttpBackend: SttpBackend[F, Nothing, WebSocketHandler]): F[File] = {
    val ivyCache = new File(new File(new File(cacheDir, "dependencies"), mode), name)

    val outFile = path.toList.foldLeft(ivyCache)(new File(_, _))

    Cache.cacheDownload(ivyCache, outFile, lock) { tempFile =>
      basicRequest.get(baseUrl.path((baseUrl.path.filter(_.nonEmpty) ++ path.toList)))
        .response(asByteArray)
        .send()
        .flatMap { response => Sync[F].fromEither(response.body.leftMap { new RuntimeException(_) }) }
        .flatMap { data =>
          blocker.delay {
            Files.write(tempFile.toPath, data)
          }
        }
    }
  }

}
