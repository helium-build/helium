package dev.helium_build.proxy

import java.io.File
import java.nio.file.Files

import cats.data.Kleisli
import cats.effect.concurrent.Semaphore
import cats.effect.{Blocker, ConcurrentEffect, ContextShift, Resource, Sync, Timer}
import cats.implicits._
import dev.helium_build.conf.RepoConfig
import dev.helium_build.record.{ArtifactSaver, Recorder}
import sttp.model.Uri
import sttp.client.asynchttpclient.cats.AsyncHttpClientCatsBackend
import fs2.Stream
import org.http4s.client.blaze.BlazeClientBuilder
import org.http4s.server.blaze.BlazeServerBuilder
import org.http4s.server.middleware.Logger
import fs2.Stream
import org.apache.commons.io.IOUtils
import org.http4s.{HttpRoutes, Request, Response}
import org.http4s.server.Server
import sttp.client.SttpBackend
import sttp.client.asynchttpclient.WebSocketHandler

import scala.concurrent.ExecutionContext.global

object ProxyServer {

  def serverResource[F[_]: ConcurrentEffect](recorder: Recorder[F], artifact: ArtifactSaver[F], blocker: Blocker)(implicit T: Timer[F], C: ContextShift[F]): Resource[F, Server[F]] = {
    Resource.liftF(
      AsyncHttpClientCatsBackend().flatMap { implicit sttpBackend =>
        for {
          conf <- recorder.repoConfig

          mavenRoutes <- conf.repo.flatMap { _.maven }.getOrElse(Nil).traverse { mavenRepo =>
            for {
              lock <- Semaphore[F](1)
              parsedUri <- Sync[F].delay {
                import sttp.client._
                uri"${mavenRepo.url}"
              }
            } yield MavenIvyRoutes.routes[F](recorder, blocker, lock, "maven", mavenRepo.name, parsedUri)
          }

          nugetRoutes <- conf.repo.flatMap { _.nuget }.getOrElse(Nil).traverse { nugetRepo =>
            for {
              lock <- Semaphore[F](1)
              parsedUri <- Sync[F].delay {
                import sttp.client._
                uri"${nugetRepo.url}"
              }
            } yield NuGetRoutes.routes[F](recorder, artifact, blocker, lock, nugetRepo.name, parsedUri)
          }

          npmRoutes <- conf.repo.flatMap { _.npm }.toList.traverse { npmRepo =>
            for {
              lock <- Semaphore[F](1)
              parsedUri <- Sync[F].delay {
                import sttp.client._
                uri"${npmRepo.registry}"
              }
            } yield NpmRoutes.routes[F](recorder, artifact, blocker, lock, parsedUri)
          }

          artifactRoutes = ArtifactRoutes.routes[F](artifact)

        } yield (mavenRoutes ++ nugetRoutes ++ npmRoutes :+ artifactRoutes)
          .reduceOption(_.combineK(_))
          .getOrElse { HttpRoutes.of[F](PartialFunction.empty) }
      }
    ).flatMap { httpRoutes =>
      import org.http4s.implicits._

      val finalHttpApp = Logger.httpApp(true, true)(httpRoutes.orNotFound)

      BlazeServerBuilder[F]
        .bindAny()
        .withHttpApp(finalHttpApp)
        .resource
    }
  }
}