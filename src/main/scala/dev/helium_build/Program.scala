package dev.helium_build

import java.io.File
import java.nio.charset.StandardCharsets
import java.nio.file.Files

import cats.effect.Blocker
import cats.implicits._
import dev.helium_build.build.BuildSchema
import dev.helium_build.conf.Config
import dev.helium_build.docker._
import dev.helium_build.proxy.ProxyServer
import dev.helium_build.record._
import dev.helium_build.sdk._
import dev.helium_build.util.Temp
import org.apache.log4j.{Appender, BasicConfigurator, Level, Logger}
import org.fusesource.scalate.{TemplateEngine, TemplateSource}
import zio.blocking.Blocking
import zio.clock.Clock
import zio._
import zio.interop.catz._
import io.circe.syntax._
import org.apache.log4j.spi.{Filter, LoggingEvent}

import scala.jdk.CollectionConverters._

object Program extends App {

  override def run(args: List[String]): ZIO[Environment, Nothing, Int] =
    runImpl(args).as(0).catchAll { i => IO.succeed(i) }

  private def runImpl(value: List[String]): ZIO[Environment, Int, Unit] = for {
    _ <- IO.effectTotal {
      BasicConfigurator.configure()
      val logger = Logger.getRootLogger
      logger.setLevel(Level.WARN)
      logger.getAllAppenders.asScala.foreach {
        case appender: Appender =>
          appender.addFilter(new Filter {
            override def decide(event: LoggingEvent): Int = {
              if(event.getLoggerName == "org.fusesource.scalate.util.ClassPathBuilder")
                Filter.DENY
              else
                Filter.NEUTRAL
            }
          })

        case _ => ()
      }
    }
    appDir <- IO.effectTotal { new File(".").getAbsoluteFile }
    confDir <- IO.effectTotal { new File(appDir, "conf") }
    cacheDir <- IO.effectTotal { new File(appDir, "cache") }
    sdkDir <- IO.effectTotal { new File(appDir, "sdks") }
    _ <- (value match {
      case "build-once" :: schemaFile :: workDir :: outputDir :: Nil =>
        runBuild(
          recordMode = RecordMode.Null(new File(workDir), new File(schemaFile)),
          confDir = confDir,
          cacheDir = cacheDir,
          sdkDir = sdkDir,
          outputDir = new File(outputDir),
        )

      case "build" :: archiveFile :: schemaFile :: workDir :: outputDir :: Nil =>
        runBuild(
          recordMode = RecordMode.Archive(new File(workDir), new File(schemaFile), new File(archiveFile)),
          confDir = confDir,
          cacheDir = cacheDir,
          sdkDir = sdkDir,
          outputDir = new File(outputDir),
        )

      case "replay" :: archiveFile :: outputDir :: Nil =>
        runBuild(
          recordMode = RecordMode.Replay(new File(archiveFile)),
          confDir = confDir,
          cacheDir = cacheDir,
          sdkDir = sdkDir,
          outputDir = new File(outputDir),
        )

      case _ => IO.fail(new RuntimeException("Build env file not specified."))
    })
      .flatMapError { error =>
        IO.effectTotal { error.printStackTrace() }.as(1)
      }
  } yield ()

  private def resolveSdkEnv(containerSdkDir: String)(envValue: EnvValue): String =
    envValue match {
      case EnvValue.OfString(value) => value
      case EnvValue.Concat(values) => values.map(resolveSdkEnv(containerSdkDir)).mkString
      case EnvValue.SdkDirectory => containerSdkDir
    }


  private def runBuild(recordMode: RecordMode, confDir: File, cacheDir: File, sdkDir: File, outputDir: File): ZIO[Blocking with Clock, Throwable, Unit] =
    (
      for {
        conf <- ZManaged.fromEffect(
          ZIO.accessM[Blocking] { _.blocking.effectBlocking { Files.readString(new File(confDir, "helium-conf.toml").toPath) } }
            .flatMap { confData =>
              IO.fromEither(Config.parse(confData).leftMap { new RuntimeException(_) })
            }
        )

        recorder <- Recorder.from[Blocking with Clock](
          cacheDir = cacheDir,
          sdkDir = sdkDir,
          recordMode
        )

        artifact = new FSArtifactSaver[Blocking with Clock](outputDir)

        buildSchema <- ZManaged.fromEffect(recorder.schema)

        sdks <- ZManaged.fromEffect(recorder.availableSdks.runCollect)
        sdkInstallManager <- ZManaged.fromEffect(recorder.sdkInstallManager)

        launchProps <- getDockerLaunchProps(sdks = sdks, workDir = recorder.workDir, conf)(sdkInstallManager)(buildSchema)

        port <- runProxyServer(recorder, artifact, cacheDir, confDir)

        socketFile <- runSocatProxy(port)

      } yield launchProps.copy(
        sockets = launchProps.sockets :+ (socketFile.toString -> "/helium/helium.sock"),
      )
    ).use(Launcher.run)


  private def getDockerLaunchProps(sdks: List[SdkInfo], workDir: File, config: Config)(sdkInstallManager: SdkInstallManager)(schema: BuildSchema): ZManaged[Blocking, Throwable, LaunchProperties] =
    ZManaged.fromEffect(PlatformInfo.current)
      .flatMap { currentPlatform =>
        schema.sdk
          .foldLeftM(LaunchProperties(
            dockerImage = "debian",
            command = schema.build.command,
            env = Map(),
            pathDirs = Seq("/usr/local/bin", "/usr/bin", "/bin", "/helium/bin"),
            sdkDirs = Seq(),
            workDir = workDir,
            configFiles = Seq(),
            sockets = Seq(),
          )) { (props, requiredSdk) =>
            for {
              sdk <- ZManaged.fromEffect(IO.fromEither(
                sdks
                  .find { sdk => sdk.matches(requiredSdk) && sdk.matchesPlatform(currentPlatform) }
                  .toRight { new RuntimeException(s"Could not find sdk ${requiredSdk}") }
              ))

              (sdkHash, sdkInstallDir) <- ZManaged.fromEffect(sdkInstallManager.getInstalledSdkDir(sdk))

              sdkConfigFiles <- sdk.configFileTemplates
                .getOrElse(Map.empty)
                .toList
                .traverse {
                  case (fileName, _) if fileName.contains(':') =>
                    ZManaged.fail(new RuntimeException("SDK config filenames may not contain colons"))

                  case (fileName, template) =>
                    ZManaged.make(
                      ZIO.accessM[Blocking] { _.blocking.effectBlocking { File.createTempFile("helium-conf-", null) } }
                    ) { file =>
                      ZIO.accessM[Blocking] { _.blocking.effectBlocking { file.delete() }.orDie }
                    }.flatMap { file =>
                      val templateEngine = new TemplateEngine()
                      val templatedData = templateEngine.layout(
                        TemplateSource.fromText("config.mustache", template),
                        config.createMap
                      )

                      ZManaged.fromEffect(ZIO.accessM[Blocking] { _.blocking.effectBlocking {
                        Files.writeString(file.toPath, templatedData, StandardCharsets.UTF_8)
                        file.toString -> fileName
                      } })
                    }
                }


              containerSdkDir = "/sdk/" + sdkHash
            } yield props.copy(
              env = props.env ++ sdk.env.view.mapValues(resolveSdkEnv(containerSdkDir)).toMap,
              pathDirs = sdk.pathDirs.map { containerSdkDir + "/" + _ } ++ props.pathDirs,
              sdkDirs = props.sdkDirs :+ ((containerSdkDir, sdkInstallDir)),
              configFiles = props.configFiles ++ sdkConfigFiles
            )
          }
      }

  private def runProxyServer[R <: Blocking with Clock](recorder: ZIORecorder[R], artifact: ZIOArtifactSaver[R], cacheDir: File, confDir: File): ZManaged[R, Throwable, Int] =
    for {
      executor <- ZManaged.fromEffect(ZIO.accessM[R] { _.blocking.blockingExecutor })
      runtime <- ZManaged.fromEffect(ZIO.runtime[R])

      server <- {
        val blocker = Blocker.liftExecutionContext(executor.asEC)
        implicit val concurrentEffectInst = zio.interop.catz.taskEffectInstance(runtime)
        implicit val timerInstance = zio.interop.catz.zioTimer[R, Throwable]


        ProxyServer.serverResource[RIO[R, *]](recorder, artifact, blocker, cacheDir, confDir).toManaged
      }


    } yield server.address.getPort

  private def runSocatProxy(port: Int): ZManaged[Blocking, Throwable, File] =
    Temp.createTempPath(
      ZIO.accessM[Blocking] { _.blocking.effectBlocking { Files.createTempDirectory("helium-socket-") } }
    )
      .flatMap { socketDir =>
        ZManaged.make(
          IO.effect {
            new ProcessBuilder("socat", s"UNIX-LISTEN:${socketDir.toString}/helium.sock,fork,mode=666", s"TCP:localhost:$port")
              .redirectOutput(ProcessBuilder.Redirect.DISCARD)
              .redirectError(ProcessBuilder.Redirect.DISCARD)
              .start()
          }
        ) { process =>
          IO.effectTotal { process.destroy() }
        }.map { _ =>
          new File(socketDir.toFile, "helium.sock")
        }
      }

}
