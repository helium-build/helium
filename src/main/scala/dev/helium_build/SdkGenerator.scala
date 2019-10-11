package dev.helium_build

import java.io.File

import com.softwaremill.sttp.asynchttpclient.zio._
import com.softwaremill.sttp.circe._
import com.softwaremill.sttp._
import dev.helium_build.sdk._
import io.circe
import io.circe.generic.auto._
import org.apache.commons.io.{FileUtils, FilenameUtils}
import zio.blocking.Blocking
import zio.console._
import zio.stream._
import zio._


object SdkGenerator extends App {

  override def run(args: List[String]): ZIO[Environment, Nothing, Int] =
    (
      for {
        _ <- ZIO.accessM[Blocking] { _.blocking.effectBlocking { FileUtils.cleanDirectory(sdkDir) } }
        _ <- Stream(

          OpenJDK,
          SBT,

          DotNet,

          NodeJS,

        ).foreach(runCreator)
      } yield ()
    )
      .as(0)
      .catchAll { ex =>
        IO.effectTotal(ex.printStackTrace()).as(1)
      }


  private val sdkDir = new File(new File(".").getCanonicalFile, "sdks")

  private def runCreator(creator: SDKCreator): RIO[Blocking with Console, Unit] =
    putStrLn(s"Generating SDKs for ${creator.name}") *>
    creator.sdks.foreach { case (path, sdk) =>
      SdkLoader.saveSdk(sdk, new File(sdkDir, path))
    }



  private implicit val sttpBackend = AsyncHttpClientZioBackend()

  private def makeSttpJSONTask[A](r: Task[Response[Either[DeserializationError[circe.Error], A]]]) =
    r
      .flatMap { resp => IO.fromEither(resp.body).mapError(new RuntimeException(_)) }
      .flatMap { resp => IO.fromEither(resp).mapError { _.error } }

  private def makeSttpStringTask[A](uri: Uri)(r: Uri => Task[Response[A]]) =
    r(uri)
      .flatMap { resp => IO.fromEither(resp.body).mapError { msg => new RuntimeException(uri.toString() + msg) } }


  private sealed trait SDKCreator {
    val name: String
    def sdks: ZStream[Console, Throwable, (String, SdkInfo)]
  }

  private object OpenJDK extends SDKCreator {

    override val name: String = "AdoptOpenJDK"

    private final case class OpenJDKRelease
    (
      release_name: String,
      release_link: String,
      timestamp: String,
      release: Boolean,
      binaries: Seq[OpenJDKBinary]
    )

    private final case class OpenJDKBinary
    (
      os: String,
      architecture: String,
      binary_name: String,
      binary_link: String,
      checksum_link: String,
      version: String,
      version_data: BinVersion,
    )

    private final case class BinVersion
    (
      openjdk_version: String,
      semver: String,
    )

    private def getAdoptOpenJDKRelease(uri: Uri): RIO[Console, OpenJDKRelease] = for {
      _ <- ZIO.accessM[Console] { _.console.putStrLn(s"release API URL: ${uri.toString()}") }

      release <-
        makeSttpJSONTask(
          sttp.get(uri)
            .response(asJson[OpenJDKRelease])
            .send()
        )
    } yield release

    private val SHA256 = "^([a-fA-F0-9]{64})\\s+.".r.unanchored

    private def getSDKForBinary(release: OpenJDKRelease, binary: OpenJDKBinary): RIO[Console, (String, SdkInfo)] = for {
      _ <- ZIO.accessM[Console] { _.console.putStrLn(s"Creating SDK for binary ${binary.binary_name}") }
      _ <- ZIO.accessM[Console] { _.console.putStrLn(s"checksum URL: ${binary.checksum_link}") }
      shaFileContent <- makeSttpStringTask(Uri(java.net.URI.create(binary.checksum_link)))(sttp.get(_).send())
      sha256 <- shaFileContent match {
        case SHA256(sha256) => IO.succeed(sha256)
        case _ => IO.fail(new RuntimeException(s"Invalid SHA256 from ${binary.checksum_link}"))
      }

      os <- IO.fromEither(SdkOperatingSystem.fromString(binary.os))
      arch <- IO.fromEither(SdkArch.fromString(binary.architecture))

    } yield s"jdk/jdk${binary.version}/${FilenameUtils.removeExtension(FilenameUtils.removeExtension(binary.binary_name))}.json" -> SdkInfo(
      implements = Seq("jdk", s"jdk${binary.version}"),
      version = binary.version_data.semver,
      os = Some(os),
      architecture = Some(arch),
      setupSteps = Seq(
        SdkDownload(binary.binary_link, binary.binary_name, SdkHash.Sha256(sha256)),
        SdkExtract(binary.binary_name, "."),
        SdkDelete(binary.binary_name),
      ),
      pathDirs = Seq(release.release_name + "/bin"),
      env = Map(
        "JAVA_HOME" -> EnvValue.Concat(Seq(EnvValue.SdkDirectory, EnvValue.OfString("/" + release.release_name)))
      ),
    )

    private def urls: Seq[Uri] =
      for {
        ver <- Seq(8, 9, 10, 11, 12, 13)
        os <- Seq("linux", "windows")
        arch <- os match {
          case "linux" => Seq("x64", "aarch64", "ppc64le", "s390x") ++ (if(ver == 8 || ver >= 10) Seq("arm") else Seq())
          case "windows" => Seq("x64") ++ (if(ver == 8 || ver >= 11) Seq("x32") else Seq())
          case _ => Seq()
        }
      } yield uri"https://api.adoptopenjdk.net/v2/info/releases/openjdk$ver?openjdk_impl=hotspot&release=latest&type=jdk&os=$os&arch=$arch"


    override def sdks: ZStream[Console, Throwable, (String, SdkInfo)] =
      Stream.fromIterable(urls)
        .mapM(getAdoptOpenJDKRelease)
        .flatMap { release =>
          Stream.fromIterable(release.binaries)
            .filter { binary => binary.os == "linux" || binary.os == "windows" }
            .mapM { binary => getSDKForBinary(release, binary) }
        }
  }

  private object SBT extends SDKCreator {

    private val version = "1.3.2"

    override val name: String = "sbt"

    override def sdks: ZStream[Console, Throwable, (String, SdkInfo)] =
      ZStream(
        "sbt/sbt-1.3.2.json" -> SdkInfo(
          implements = Seq("sbt"),
          version = version,
          os = None,
          architecture = None,
          setupSteps = Seq(
            SdkDownload(s"https://piccolo.link/sbt-$version.tgz", s"sbt-$version.tgz", SdkHash.Sha256("ed8cef399129895ad0d757eea812b3f95830a36fa838f8ede1c6cdc2294f326f")),
            SdkExtract(s"sbt-$version.tgz", "."),
            SdkDelete(s"sbt-$version.tgz"),
          ),
          pathDirs = Seq("sbt/bin"),
          env = Map.empty,
          configFileTemplates = Some(Map(
            "~/.sbt/repositories" ->
              """
                |[repositories]
                |{{#repo_maven}}
                |{{{name}}}: {{{url}}}
                |{{/repo_maven}}
                |""".stripMargin
          ))
        )
      )
  }

  private object DotNet extends SDKCreator {

    override val name: String = "dotnet"

    private val SHA512 = "^([a-fA-F0-9]{128})\\s+(.+)".r.unanchored

    private val channels = Seq(
      "3.0",
      "2.2",
      "2.1"
    )

    private def latestVersionSdk(channel: String): RIO[Console, String] =
      makeSttpStringTask(uri"https://dotnetcli.blob.core.windows.net/dotnet/Sdk/$channel/latest.version")(sttp.get(_).send())
        .map { _.linesIterator.drop(1).next().trim }

    private def latestVersionRuntime(channel: String): RIO[Console, String] =
      makeSttpStringTask(uri"https://dotnetcli.blob.core.windows.net/dotnet/Runtime/$channel/latest.version")(sttp.get(_).send())
        .map { _.linesIterator.drop(1).next().trim }

    override def sdks: ZStream[Console, Throwable, (String, SdkInfo)] = for {
      channel <- ZStream.fromIterable(channels)

      (os, osStr, ext) <- Stream(
        (SdkOperatingSystem.Windows, "win", "zip"),
        (SdkOperatingSystem.Linux, "linux", "tar.gz"),
      )

      (arch, archStr) <- Stream(
        (SdkArch.Amd64, "x64"),
        (SdkArch.X86, "x86"),
        (SdkArch.Arm, "arm"),
        (SdkArch.Aarch64, "arm64"),
      )

      (verSdk, verRuntime, shaMap) <- ZStream.fromEffect(
        for {
          verSdk <- latestVersionSdk(channel)
          verRuntime <- latestVersionRuntime(channel)
          shaFileContent <- makeSttpStringTask(uri"https://dotnetcli.blob.core.windows.net/dotnet/checksums/$verRuntime-sha.txt")(sttp.get(_).send())
          shaMap = shaFileContent.linesIterator.collect {
            case SHA512(sha512, fileName) => fileName -> sha512
          }.toMap
        } yield (verSdk, verRuntime, shaMap)
      )

      fileName = s"dotnet-sdk-$verSdk-$osStr-$archStr.$ext"
      outDir = s"dotnet-$verRuntime"

      sha512 <- ZStream.fromIterable(shaMap.get(fileName).toList)

      sdkInfo = SdkInfo(
        implements = Seq("dotnet"),
        version = verRuntime,
        os = Some(os),
        architecture = Some(arch),
        setupSteps = Seq(
          SdkDownload(s"https://dotnetcli.azureedge.net/dotnet/Sdk/$verSdk/$fileName", fileName, SdkHash.Sha512(sha512)),
          SdkExtract(fileName, outDir),
          SdkDelete(fileName),
        ),
        pathDirs = Seq(outDir),
        env = Map(
          "DOTNET_CLI_TELEMETRY_OPTOUT" -> EnvValue.OfString("1"),
        ),
        configFileTemplates = {
          val configFile =
            """<?xml version="1.0" encoding="utf-8"?>
              |<configuration>
              |    <packageSources>
              |        <clear/>
              |{{#repo_nuget}}
              |        <add key="{{name}}" value="{{url}}" />
              |{{/repo_nuget}}
              |    </packageSources>
              |
              |    <config>
              |        <add key="defaultPushSource" value="{{nuget_push_url}}" />
              |    </config>
              |</configuration>
              |""".stripMargin

          Some(Map(
            "$CONFIG/NuGet/NuGet.Config" -> configFile,
            "~/.nuget/NuGet/NuGet.Config" -> configFile,
          ))
        }
      )

    } yield s"dotnet/$verRuntime-$osStr-$archStr.json" -> sdkInfo

  }

  private object NodeJS extends SDKCreator {

    override val name: String = "node"

    private val SHA256 = "^([a-fA-F0-9]{64})\\s+(.+)".r.unanchored

    override def sdks: ZStream[Console, Throwable, (String, SdkInfo)] = for {
      ver <- Stream("12.7.0")
      shaMap <- ZStream.fromEffect(
        for {
          shaFileContent <- makeSttpStringTask(uri"https://nodejs.org/dist/v$ver/SHASUMS256.txt")(sttp.get(_).send())
        } yield shaFileContent.linesIterator.collect {
          case SHA256(sha256, fileName) => fileName -> sha256
        }.toMap
      )

      (os, osStr, arch, archStr, ext) <- Stream(
        (SdkOperatingSystem.Windows, "win", SdkArch.Amd64, "x64", "zip"),
        (SdkOperatingSystem.Windows, "win", SdkArch.X86, "x86", "zip"),
        (SdkOperatingSystem.Linux, "linux", SdkArch.Amd64, "x64", "tar.gz"),
        (SdkOperatingSystem.Linux, "linux", SdkArch.Arm, "armv7l", "tar.gz"),
        (SdkOperatingSystem.Linux, "linux", SdkArch.Aarch64, "arm64", "tar.gz"),
        (SdkOperatingSystem.Linux, "linux", SdkArch.Ppc64le, "ppc64le", "tar.gz"),
        (SdkOperatingSystem.Linux, "linux", SdkArch.S390x, "s390x", "tar.gz"),
      )

      sdkInfo <- ZStream.fromEffect({
        val archiveDir = s"node-v$ver-$osStr-$archStr"
        val fileName = s"$archiveDir.$ext"
        for {
          sha256 <- IO.fromEither(shaMap.get(fileName).toRight(new RuntimeException(s"Could not find hash for file $fileName")))
        } yield SdkInfo(
          implements = Seq("node"),
          version = ver,
          os = Some(os),
          architecture = Some(arch),
          setupSteps = Seq(
            SdkDownload(s"https://nodejs.org/dist/v$ver/$fileName", fileName, SdkHash.Sha256(sha256)),
            SdkExtract(fileName, "."),
            SdkDelete(fileName),
          ),
          pathDirs = Seq(archiveDir + "/bin"),
          env = Map(),
          configFileTemplates = Some(Map(
            "~/.npmrc" -> "registry=http://localhost:9000/npm/\n"
          ))
        )
      })

    } yield s"nodejs/v$ver-$osStr-$archStr.json" -> sdkInfo
  }

}
