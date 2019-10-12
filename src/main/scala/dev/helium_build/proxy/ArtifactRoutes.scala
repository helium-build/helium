package dev.helium_build.proxy

import java.io.File

import cats.effect.{Blocker, Sync}
import dev.helium_build.record.{ArtifactAlreadyExistsException, ArtifactSaver, Recorder}
import org.http4s.HttpRoutes
import org.http4s.dsl.Http4sDsl
import org.http4s.multipart.Multipart
import cats.implicits._

object ArtifactRoutes {

  def routes[F[_]: Sync](artifact: ArtifactSaver[F]): HttpRoutes[F] = {
    val dsl = new Http4sDsl[F]{}
    import dsl._

    HttpRoutes.of[F] {
      case request @ PUT -> Root / "artifact" / fileName =>
        request.decode[Multipart[F]] { m =>
          m.parts.headOption match {
            case Some(part) =>
              artifact.saveArtifact(fileName, part.body)
                .flatMap { _ => Ok() }
                .recoverWith {
                  case _: ArtifactAlreadyExistsException =>
                    Conflict()
                }

            case None => BadRequest()
          }
        }

    }
  }

}
