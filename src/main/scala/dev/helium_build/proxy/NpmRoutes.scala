package dev.helium_build.proxy

import java.io.{File, IOException}
import java.util.Base64

import cats.implicits._
import cats.effect.{Async, Blocker, ContextShift, Sync}
import cats.effect.concurrent.Semaphore
import com.github.zafarkhaja.semver.Version
import dev.helium_build.record.{ArtifactSaver, Recorder}
import com.softwaremill.sttp.SttpBackend
import org.http4s.{HttpRoutes, Request, Response, StaticFile}
import org.http4s.dsl.Http4sDsl
import org.http4s.dsl.impl.Path
import org.http4s.headers._
import com.softwaremill.sttp.{SttpBackend, asFile, sttp, Uri => SttpUri}
import io.circe.{HCursor, Json, JsonObject}
import org.http4s.circe._
import org.http4s.multipart.Multipart

object NpmRoutes {


  def routes[F[_]: Async : ContextShift](recorder: Recorder[F], artifact: ArtifactSaver[F], blocker: Blocker, lock: Semaphore[F], cacheDir: File, registry: SttpUri)(implicit sttpBackend: SttpBackend[F, Nothing]): HttpRoutes[F] = {
    val dsl = new Http4sDsl[F]{}
    import dsl._
    HttpRoutes.of[F] {
      case GET -> Root / "npm" / packageName if validateName(packageName) =>
        recorder.recordTransientMetadata("npm/" + packageName)(
          fetchMetadataAllVersions(registry, Seq(packageName))
        )
          .flatMap(Ok(_))

      case GET -> Root / "npm" / scope / packageName
        if scope.startsWith("@") && validateName(scope) && validateName(packageName) =>
        recorder.recordTransientMetadata("npm/" + scope + "/" + packageName)(
          fetchMetadataAllVersions(registry, Seq(scope, packageName))
        )
          .flatMap(Ok(_))

      case GET -> Root / "npm" / packageName / version if validateName(packageName) && validateVersion(version) =>
        recorder.recordTransientMetadata("npm/" + packageName + "/" + version)(
          fetchMetadata(registry, Seq(packageName), version)
        )
          .flatMap(Ok(_))

      case GET -> Root / "npm" / scope / packageName / version
        if scope.startsWith("@") && validateName(scope) && validateName(packageName) && validateVersion(version) =>
        recorder.recordTransientMetadata("npm/" + scope + "/" + packageName + "/" + version)(
          fetchMetadata(registry, Seq(scope, packageName), version)
        )
          .flatMap(Ok(_))

      case request @ GET -> Root / "npm" / packageName / "-" / version ~ "tgz"
        if validateName(packageName) && validateVersion(version) =>
        recorder.recordArtifact("npm/" + packageName + "/" + version + ".tgz")(
          resolveArtifact(lock, cacheDir, registry, Seq(packageName), version)
        )
          .flatMap { file =>
            StaticFile.fromFile(file, blocker, Some(request)).getOrElseF(NotFound())
          }

      case request @ GET -> Root / "npm" / scope / packageName / "-" / version ~ "tgz"
        if scope.startsWith("@") && validateName(scope) && validateName(packageName) && validateVersion(version) =>
        recorder.recordArtifact("npm/" + scope + "/" + packageName + "/" + version + ".tgz")(
          resolveArtifact(lock, cacheDir, registry, Seq(scope, packageName), version)
        )
          .flatMap { file =>
            StaticFile.fromFile(file, blocker, Some(request)).getOrElseF(NotFound())
          }

      case request @ PUT -> Root / "npm" / packageName if validateName(packageName) =>
        publishArtifact(request)(artifact)(Ok())

      case request @ PUT -> Root / "npm" / scope / packageName if validateName(scope) && validateName(packageName) =>
        publishArtifact(request)(artifact)(Ok())

    }
  }

  private def validateName(name: String): Boolean =
    name.nonEmpty && !name.startsWith(".") && !name.contains("\\")

  private def validateVersion(version: String): Boolean =
    try { Version.valueOf(version); true } catch { case _: Exception => false }

  private def resolveArtifact[F[_]: Sync](lock: Semaphore[F], cacheDir: File, registry: SttpUri, packageName: Seq[String], version: String)(implicit sttpBackend: SttpBackend[F, Nothing]): F[File] = {
    val npmCache = new File(new File(cacheDir, "dependencies"), "npm")
    val outFile = new File(packageName.foldLeft(npmCache) { new File(_, _) }, version + ".tgz")

    Cache.cacheDownload(npmCache, outFile, lock) { tempFile =>
      sttp
        .get(registry.path(registry.path.filter { _.nonEmpty } ++ packageName :+ version))
        .send()
        .flatMap { response => Sync[F].fromEither(response.body.leftMap(new RuntimeException(_))) }
        .flatMap { body =>
          Sync[F].fromEither(io.circe.parser.parse(body))
        }
        .flatMap { metadata =>
          Sync[F].fromEither(metadata.hcursor.downField("dist").get[String]("tarball"))
        }
        .flatMap { tarball =>
          import com.softwaremill.sttp._

          sttp.get(uri"$tarball")
            .response(asFile(tempFile, overwrite = true))
            .send()
            .map { _ => () }
        }
    }
  }

  private def fetchMetadataAllVersions[F[_]: Sync](registry: SttpUri, packageName: Seq[String])(implicit sttpBackend: SttpBackend[F, Nothing]): F[Json] =
    sttp
      .get(registry.path(registry.path.filter { _.nonEmpty } ++ packageName))
      .send()
      .flatMap { response => Sync[F].fromEither(response.body.leftMap(new RuntimeException(_))) }
      .flatMap { body =>
        Sync[F].fromEither(
          io.circe.parser.parse(body).toOption
            .flatMap { updateDistTarballVersions(_, packageName) }
            .toRight { new RuntimeException("Invalid package metadata") }
        )
      }

  private def fetchMetadata[F[_]: Sync](registry: SttpUri, packageName: Seq[String], version: String)(implicit sttpBackend: SttpBackend[F, Nothing]): F[Json] =
    sttp
      .get(registry.path(registry.path.filter { _.nonEmpty } ++ packageName :+ version))
      .send()
      .flatMap { response => Sync[F].fromEither(response.body.leftMap(new RuntimeException(_))) }
      .flatMap { body =>
        Sync[F].fromEither(
          io.circe.parser.parse(body).toOption
            .flatMap { updateDistTarball(_, packageName, version) }
            .toRight { new RuntimeException("Invalid package metadata") }
        )
      }


  private def updateDistTarball(metadata: Json, packageName: Seq[String], version: String): Option[Json] =
    metadata.hcursor.downField("dist").downField("tarball").withFocus { _ =>
      Json.fromString("http://localhost:9000/npm/" + packageName.mkString("/") + "/-/" + version + ".tgz")
    }.top


  private def updateDistTarballVersions(metadata: Json, packageName: Seq[String]): Option[Json] =
    metadata.hcursor.downField("versions").withFocusM { versions =>
      versions.asObject.flatMap { versionsObj =>
        versionsObj
          .toVector
          .traverse {
            case (ver, verMetadata) =>
              updateDistTarball(verMetadata, packageName, ver).map { ver -> _ }
          }
          .map(JsonObject.fromIterable)
          .map(Json.fromJsonObject)
      }
    }.flatMap { _.top }

  private def publishArtifact[F[_]: Sync](request: Request[F])(artifact: ArtifactSaver[F])(response: F[Response[F]]): F[Response[F]] =
    request.decode[Json] { artifactJson =>
      Sync[F].fromEither(artifactJson.hcursor.downField("_attachments").as[JsonObject]).flatMap { attachments =>
        attachments.toMap.toVector.traverse_ {
          case (name, attachment) =>
            Sync[F].fromEither(attachment.hcursor.downField("data").as[String])
              .flatMap { dataB64 =>
                Sync[F].delay { Base64.getDecoder.decode(dataB64) }
              }
              .flatMap { data =>
                artifact.saveArtifact(name, fs2.Stream.chunk(fs2.Chunk.bytes(data)))
              }

        }
          .flatMap { _ => response }
      }
    }
}
