package dev.helium_build.proxy

import java.io.File
import java.nio.charset.StandardCharsets
import java.util.zip.ZipFile

import cats.effect.concurrent.Semaphore
import cats.effect.{Async, Blocker, ContextShift, Sync}
import cats.implicits._
import dev.helium_build.record.{ArtifactAlreadyExistsException, ArtifactSaver, Recorder}
import io.circe.Json
import org.http4s.{HttpRoutes, MediaType, StaticFile}
import org.http4s.dsl.Http4sDsl
import org.http4s.circe._
import sttp.client.{SttpBackend, asFile, basicRequest}
import sttp.model.{Uri => SttpUri}
import sttp.client.circe._
import io.circe.generic.JsonCodec
import org.apache.commons.io.IOUtils
import org.apache.commons.io.input.BOMInputStream
import org.http4s.multipart.Multipart
import sttp.client.asynchttpclient.WebSocketHandler

import scala.jdk.CollectionConverters._

object NuGetRoutes {

  def routes[F[_]: Sync : ContextShift](recorder: Recorder[F], artifact: ArtifactSaver[F], blocker: Blocker, lock: Semaphore[F], name: String, nugetUri: SttpUri)(implicit sttpBackend: SttpBackend[F, Nothing, WebSocketHandler]): HttpRoutes[F] = {
    val dsl = new Http4sDsl[F]{}
    import dsl._

    HttpRoutes.of[F] {
      case GET -> Root / "nuget" / `name` / "v3" / "index.json" =>
        Ok(
          Json.obj(
            "version" -> Json.fromString("3.0.0"),
            "resources" -> Json.arr(
              Json.obj(
                "@id" -> Json.fromString(s"http://localhost:9000/nuget/$name/v3/package"),
                "@type" -> Json.fromString("PackageBaseAddress/3.0.0"),
              ),
              Json.obj(
                "@id" -> Json.fromString(s"http://localhost:9000/nuget/publish"),
                "@type" -> Json.fromString("PackagePublish/2.0.0"),
              ),
              Json.obj(
                "@id" -> Json.fromString(s"http://localhost:9000/nuget/$name/v3/registration"),
                "@type" -> Json.fromString("RegistrationsBaseUrl/3.0.0-rc"),
              ),
              Json.obj(
                "@id" -> Json.fromString(s"http://localhost:9000/nuget/$name/v3/query"),
                "@type" -> Json.fromString("SearchQueryService/3.0.0-rc"),
              ),
            ),
          )
        )

      case (GET | HEAD) -> Root / "nuget" / `name` / "v3" / "package" / packageId / "index.json" =>
        recorder.recordTransientMetadata("nuget/" + name + "/v3/package/" + packageId + "/index.json")(
          getPackageIndex(packageId, nugetUri)
        )
          .flatMap(Ok(_))

      case request @ GET -> Root / "nuget" / `name` / "v3" / "package" / packageId / packageVersion / fileName ~ "nupkg"
        if isValidName(packageId) &&
          isValidName(packageVersion) &&
          fileName == packageId + "." + packageVersion =>
        recorder.recordArtifact("nuget/" + name + "/" + packageId + "/" + packageVersion + "/" + fileName + ".nupkg") { cacheDir =>
          getNupkg(lock, cacheDir, name, packageId, packageVersion, nugetUri)
        }
          .flatMap { nupkg =>
            StaticFile.fromFile(nupkg, blocker, Some(request)).getOrElseF(NotFound())
          }

      case HEAD -> Root / "nuget" / `name` / "v3" / "package" / packageId / packageVersion / fileName ~ "nupkg"
        if isValidName(packageId) &&
          isValidName(packageVersion) &&
          fileName == packageId + "." + packageVersion =>
        recorder.recordArtifact("nuget/" + name + "/" + packageId + "/" + packageVersion + "/" + fileName + ".nupkg") { cacheDir =>
          getNupkg(lock, cacheDir, name, packageId, packageVersion, nugetUri)
        }
          .flatMap { _ =>
            Ok()
          }

      case GET -> Root / "nuget" / `name` / "v3" / "package" / packageId / packageVersion / fileName ~ "nuspec"
        if isValidName(packageId) &&
          isValidName(packageVersion) &&
          fileName == packageId =>
        recorder.recordArtifact("nuget/" + name + "/" + packageId + "/" + packageVersion + "/" + fileName + ".nupkg") { cacheDir =>
          getNupkg(lock, cacheDir, name, packageId, packageVersion, nugetUri)
        }
          .flatMap { nupkg =>
            readNuSpec(blocker, nupkg, removeBom = false).flatMap(Ok(_))
          }

      case HEAD -> Root / "nuget" / `name` / "v3" / "package" / packageId / packageVersion / fileName ~ "nuspec"
        if isValidName(packageId) &&
          isValidName(packageVersion) &&
          fileName == packageId =>
        recorder.recordArtifact("nuget/" + name + "/" + packageId + "/" + packageVersion + "/" + fileName + ".nupkg") { cacheDir =>
          getNupkg(lock, cacheDir, name, packageId, packageVersion, nugetUri)
        }
          .flatMap { _ =>
            Ok()
          }

      case request @ (PUT -> Root / "nuget" / "publish" | PUT -> Root / "nuget" / "publish" / "") =>
        request.decode[Multipart[F]] { m =>
          m.parts.headOption match {
            case Some(part) =>
              artifact.saveArtifact(part.body) { file =>
                readNuSpec(blocker, file, removeBom = true).flatMap { xmlStr =>
                  Sync[F].delay {
                    val xml = scala.xml.XML.loadString(xmlStr)
                    val id = (xml \ "metadata" \ "id").text
                    val version = (xml \ "metadata" \ "version").text

                    if(id.isEmpty || version.isEmpty)
                      throw new RuntimeException("Could not find package version")
                    else
                      id + "." + version + ".nupkg"
                  }
                }
              }
                .flatMap { _ => Ok() }
                .recoverWith {
                  case _: ArtifactAlreadyExistsException =>
                    Conflict()
                }

            case None => BadRequest()
          }
        }

      case DELETE -> Root / "nuget" / `name` / "v3" / "publish" / _ / _ =>
        Forbidden()

      case POST -> Root / "nuget" / `name` / "v3" / "publish" / _ / _ =>
        Forbidden()

      case (GET | HEAD) -> Root / "nuget" / `name` / "v3" / "registration" / packageId / "index.json" =>
        NotFound()

      case GET -> Root / "nuget" / `name` / "v3" / "query" =>
        Ok(
          Json.obj(
            "totalHits" -> Json.fromInt(0),
            "data" -> Json.arr(),
          )
        )

      case HEAD -> Root / "nuget" / `name` / "v3" / "query" =>
        Ok()


    }
  }

  private def isValidName(name: String): Boolean =
    name.matches("[a-z0-9\\_-][a-z0-9\\_\\-\\.]*")

  private def getNupkg[F[_]: Sync](lock: Semaphore[F], cacheDir: File, name: String, packageName: String, packageVersion: String, nugetUri: SttpUri)(implicit sttpBackend: SttpBackend[F, Nothing, WebSocketHandler]): F[File] = {
    val nugetCache = new File(new File(new File(cacheDir, "dependencies"), "nuget"), name)

    val outFile = new File(new File(new File(nugetCache, packageName), packageVersion), packageName + "." + packageVersion + ".nupkg")

    Cache.cacheDownload(nugetCache, outFile, lock) { tempFile =>
      nugetPackageBaseAddress(nugetUri) { baseUri =>
        basicRequest.get(baseUri.path(baseUri.path.filter { _.nonEmpty } ++ Seq(packageName, packageVersion, packageName + "." + packageVersion + ".nupkg")))
          .response(asFile(tempFile))
          .send()
          .map { _ => () }
      }
    }
  }

  private def getPackageIndex[F[_]: Sync](packageName: String, nugetUri: SttpUri)(implicit sttpBackend: SttpBackend[F, Nothing, WebSocketHandler]): F[Json] =
    nugetPackageBaseAddress(nugetUri) { baseUri =>
      for {
        response <- basicRequest.get(baseUri.path(baseUri.path.filter { _.nonEmpty } ++ Seq(packageName, "index.json"))).send()
        resultStr <- Sync[F].fromEither(response.body.left.map(new RuntimeException(_)))
        resultJson <- Sync[F].fromEither(io.circe.parser.parse(resultStr))
      } yield resultJson
    }

  private def nugetPackageBaseAddress[F[_]: Sync, A](nugetUri: SttpUri)(f: SttpUri => F[A])(implicit sttpBackend: SttpBackend[F, Nothing, WebSocketHandler]): F[A] = {
    import sttp.client._
    import io.circe.generic.auto._

    for {
      serviceIndexResponse <- basicRequest.get(nugetUri).response(asJson[NuGetServiceIndex]).send()
      serviceIndex <- Sync[F].fromEither(serviceIndexResponse.body)

      packageBaseAddress <- Sync[F].fromEither (
        serviceIndex.resources.find { _.`@type` == "PackageBaseAddress/3.0.0" }
          .toRight { new RuntimeException("Could not find PackageBaseAddress/3.0.0") }
      )

      baseUri <- Sync[F].delay { uri"${packageBaseAddress.`@id`}" }

      result <- f(baseUri)
    } yield result
  }

  private def readNuSpec[F[_] : Sync : ContextShift](blocker: Blocker, nupkg: File, removeBom: Boolean): F[String] =
    blocker.delay {
      val zipFile = new ZipFile(nupkg)
      try {
        val entry = zipFile.entries().asScala.find { _.getName.endsWith(".nuspec") }.getOrElse { throw new RuntimeException("Could not find nuspec in package.") }
        val stream = zipFile.getInputStream(entry)
        try {
          if(removeBom) {
            val bomStream = new BOMInputStream(stream)
            try IOUtils.toString(bomStream, StandardCharsets.UTF_8)
            finally bomStream.close()
          }
          else {
            IOUtils.toString(stream, StandardCharsets.UTF_8)
          }
        }
        finally {
          stream.close()
        }
      }
      finally {
        zipFile.close()
      }
    }

  private final case class NuGetServiceIndex
  (
    version: String,
    resources: List[NuGetResource]
  )

  private final case class NuGetResource
  (
    `@id`: String,
    `@type`: String,
  )

}
