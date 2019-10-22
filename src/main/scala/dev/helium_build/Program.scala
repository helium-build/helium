package dev.helium_build

import java.io.File
import java.nio.charset.StandardCharsets
import java.nio.file.Files

import cats.effect.Blocker
import cats.implicits._
import dev.helium_build.build.BuildSchema
import dev.helium_build.conf.RepoConfig
import dev.helium_build.docker._
import dev.helium_build.proxy.ProxyServer
import dev.helium_build.record._
import dev.helium_build.sdk._
import dev.helium_build.util.{ArchiveUtil, Temp}
import org.apache.log4j.{Appender, BasicConfigurator, Level, Logger}
import org.fusesource.scalate.{TemplateEngine, TemplateSource}
import zio.blocking.Blocking
import zio.clock.Clock
import zio._
import zio.interop.catz._
import io.circe.syntax._
import org.apache.commons.lang3.SystemUtils
import org.apache.log4j.spi.{Filter, LoggingEvent}
import zio.console._

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
    appDir <- IO.effectTotal {
      sys.env.get("HELIUM_BASE_DIR")
        .map { new File(_) }
        .getOrElse { new File(".") }
        .getCanonicalFile
    }
    confDir <- IO.effectTotal { new File(appDir, "conf") }
    cacheDir <- IO.effectTotal { new File(appDir, "cache") }
    sdkDir <- IO.effectTotal { new File(appDir, "sdks") }
    _ <- {
      def invalidArguments = IO.fail(new RuntimeException("Invalid arguments."))

      def handleBuildArgs(args: List[String], schemaFileOpt: Option[File], sourcesDirOpt: Option[File], outputDirOpt: Option[File], archiveFileOpt: Option[File]): RIO[Blocking with Clock with Console, Unit] =
        args match {
          case ("-h" | "--help") :: _ =>
            for {
              _ <- putStrLn("Usage: helium build [options] workDir")
              _ <- putStrLn("")
              _ <- putStrLn("Options:")
              _ <- putStrLn("    --schema file (default: workDir/build.toml or workDir/sources/build.toml)")
              _ <- putStrLn("    --sources dir (default: workDir/sources/)")
              _ <- putStrLn("    --output dir (default: workDir/output/)")
              _ <- putStrLn("    --archive file")
            } yield ()

          case "--schema" :: _ :: _ if schemaFileOpt.isDefined =>
            for {
              _ <- putStrLn("Error: Schema is specified multiple times.")
              _ <- invalidArguments
            } yield ()

          case "--schema" :: Nil =>
            for {
              _ <- putStrLn("Error: Schema requires an argument.")
              _ <- invalidArguments
            } yield ()

          case "--schema" :: file :: tail =>
            handleBuildArgs(
              args = tail,
              schemaFileOpt = Some(new File(file)),
              sourcesDirOpt = sourcesDirOpt,
              outputDirOpt = outputDirOpt,
              archiveFileOpt = archiveFileOpt,
            )

          case "--sources" :: _ :: _ if sourcesDirOpt.isDefined =>
            for {
              _ <- putStrLn("Error: Sources is specified multiple times.")
              _ <- invalidArguments
            } yield ()

          case "--sources" :: Nil =>
            for {
              _ <- putStrLn("Error: Sources requires an argument.")
              _ <- invalidArguments
            } yield ()

          case "--sources" :: dir :: tail =>
            handleBuildArgs(
              args = tail,
              schemaFileOpt = schemaFileOpt,
              sourcesDirOpt = Some(new File(dir)),
              outputDirOpt = outputDirOpt,
              archiveFileOpt = archiveFileOpt,
            )

          case "--output" :: _ :: _ if outputDirOpt.isDefined =>
            for {
              _ <- putStrLn("Error: Output is specified multiple times.")
              _ <- invalidArguments
            } yield ()

          case "--output" :: Nil =>
            for {
              _ <- putStrLn("Error: Output requires an argument.")
              _ <- invalidArguments
            } yield ()

          case "--output" :: dir :: tail =>
            handleBuildArgs(
              args = tail,
              schemaFileOpt = schemaFileOpt,
              sourcesDirOpt = sourcesDirOpt,
              outputDirOpt = Some(new File(dir)),
              archiveFileOpt = archiveFileOpt,
            )

          case "--archive" :: _ :: _ if archiveFileOpt.isDefined =>
            for {
              _ <- putStrLn("Error: Archive is specified multiple times.")
              _ <- invalidArguments
            } yield ()

          case "--archive" :: Nil =>
            for {
              _ <- putStrLn("Error: Archive requires an argument.")
              _ <- invalidArguments
            } yield ()

          case "--archive" :: file :: tail =>
            handleBuildArgs(
              args = tail,
              schemaFileOpt = schemaFileOpt,
              sourcesDirOpt = sourcesDirOpt,
              outputDirOpt = outputDirOpt,
              archiveFileOpt = Some(new File(file)),
            )

          case sw :: _ if sw startsWith "-" =>
            for {
              _ <- putStrLn(s"Error: Unknown option $sw")
              _ <- invalidArguments
            } yield ()

          case workDirStr :: Nil =>
            val workDir = new File(workDirStr)
            val outputDir = outputDirOpt.getOrElse { new File(workDir, "output") }
            val sourcesDir = sourcesDirOpt.getOrElse { new File(workDir, "sources") }

            for {
              schemaFile <- ZIO.accessM[Blocking] { _.blocking.effectBlocking {
                schemaFileOpt.getOrElse {
                  val schemaFile1 = new File(workDir, "build.toml")
                  if(schemaFile1.exists())
                    schemaFile1
                  else
                    new File(workDir, "sources/build.toml")
                }
              } }

              recorderManaged = archiveFileOpt match {
                case Some(archiveFile) =>
                  ArchiveRecorder[Blocking with Clock](
                    cacheDir = cacheDir,
                    sdkDir = sdkDir,
                    schemaFile = schemaFile,
                    archiveFile = archiveFile,
                    sourcesDir = sourcesDir,
                    confDir = confDir,
                  )

                case None =>
                  NullRecorder[Blocking with Clock](
                    cacheDir = cacheDir,
                    sdkDir = sdkDir,
                    schemaFile = schemaFile,
                    sourcesDir = sourcesDir,
                    confDir = confDir,
                  )
              }

              _ <- runBuild(recorderManaged, outputDir = outputDir, workDir = workDir)

            } yield ()

          case _ => invalidArguments
        }

      (value match {
        case "build" :: tail =>
          handleBuildArgs(tail, None, None, None, None)

        case "replay" :: archiveFile :: workDirStr :: outputDir :: Nil =>
          val workDir = new File(workDirStr)
          runBuild(
            recorderManaged = ReplayRecorder(
              archiveFile = new File(archiveFile),
              workDir = workDir,
            ),
            outputDir = new File(outputDir),
            workDir = workDir,
          )

        case _ => IO.fail(new RuntimeException("Build env file not specified."))
      })
        .flatMapError { error =>
          IO.effectTotal { error.printStackTrace() }.as(1)
        }
    }
  } yield ()

  private def resolveSdkEnv(containerSdkDir: String)(envValue: EnvValue): String =
    envValue match {
      case EnvValue.OfString(value) => value
      case EnvValue.Concat(values) => values.map(resolveSdkEnv(containerSdkDir)).mkString
      case EnvValue.SdkDirectory => containerSdkDir
    }


  private def runBuild(recorderManaged: ZManaged[Blocking with Clock, Throwable, ZIORecorder[Blocking with Clock]], outputDir: File, workDir: File): ZIO[Blocking with Clock, Throwable, Unit] =
    (
      for {
        recorder <- recorderManaged

        artifact = new FSArtifactSaver[Blocking with Clock](outputDir)

        buildSchema <- ZManaged.fromEffect(recorder.schema)

        sdks <- ZManaged.fromEffect(recorder.availableSdks.runCollect)
        sdkInstallManager <- ZManaged.fromEffect(recorder.sdkInstallManager)


        conf <- ZManaged.fromEffect(recorder.repoConfig)
        launchProps <- getDockerLaunchProps(sdks = sdks, workDir = workDir, sourcesDir = recorder.sourcesDir, conf)(sdkInstallManager)(buildSchema)

        port <- runProxyServer(recorder, artifact)

        _ <- runUnixSocketProxy(launchProps.socketDir, port)

      } yield launchProps
    ).use(Launcher.run)


  private def getDockerLaunchProps(sdks: List[SdkInfo], workDir: File, sourcesDir: File, config: RepoConfig)(sdkInstallManager: SdkInstallManager)(schema: BuildSchema): ZManaged[Blocking, Throwable, LaunchProperties] = for {
    currentPlatform <- ZManaged.fromEffect(PlatformInfo.current)

    installDir <- Temp.createTempPath(
      ZIO.accessM[Blocking] { _.blocking.effectBlocking { Files.createTempDirectory(workDir.toPath, "helium-install-") } }
    )

    socketDir <- Temp.createTempPath(
      ZIO.accessM[Blocking] { _.blocking.effectBlocking { Files.createTempDirectory(workDir.toPath, "helium-socket-") } }
    )

    props <- schema.sdk
      .foldLeftM(LaunchProperties(
        dockerImage = currentPlatform.os match {
          case SdkOperatingSystem.Linux => "helium-build/build-env:debian-buster-20190708"
          case SdkOperatingSystem.Windows => "helium-build/build-env:windows-nanoserver-1903"
        },
        command = schema.build.command,
        env = Map(),
        pathDirs = Seq(),
        sdkDirs = Seq(),
        sourcesDir = sourcesDir,
        installDir = installDir.toFile,
        socketDir = socketDir.toFile,
      )) { (props, requiredSdk) =>
        for {
          sdk <- ZManaged.fromEffect(IO.fromEither(
            sdks
              .find { sdk => sdk.matches(requiredSdk) && sdk.matchesPlatform(currentPlatform) }
              .toRight { new RuntimeException(s"Could not find sdk ${requiredSdk}") }
          ))

          (sdkHash, sdkInstallDir) <- ZManaged.fromEffect(sdkInstallManager.getInstalledSdkDir(sdk))

          _ <- sdk.configFileTemplates
            .getOrElse(Map.empty)
            .toList
            .traverse_ {
              case (fileName, _) if fileName.contains(':') =>
                ZManaged.fail(new RuntimeException("SDK config filenames may not contain colons"))

              case (fileName, template) =>
                ZManaged.fromEffect(
                  for {
                    (typeDir, path) <- getConfigFilePath(fileName)
                    normalizedFileName <- ArchiveUtil.normalizePath(path)
                    templatedData <- IO.effect {
                      val templateEngine = new TemplateEngine()
                      templateEngine.layout(
                        TemplateSource.fromText("config.mustache", template),
                        config.createMap
                      )
                    }

                    _ <- ZIO.accessM[Blocking] { _.blocking.effectBlocking {
                      val file = new File(new File(installDir.toFile, typeDir), normalizedFileName)
                      file.getParentFile.mkdirs()
                      Files.writeString(file.toPath, templatedData, StandardCharsets.UTF_8)
                    } }

                  } yield ()
                )
            }


          containerSdkDir = sdkHash
        } yield props.copy(
          env = props.env ++ sdk.env.view.mapValues(resolveSdkEnv(containerSdkDir)).toMap,
          pathDirs = sdk.pathDirs.map { containerSdkDir + "/" + _ } ++ props.pathDirs,
          sdkDirs = props.sdkDirs :+ ((containerSdkDir, sdkInstallDir)),
        )
      }

  } yield props

  private def getConfigFilePath(fileName: String): Task[(String, String)] =
    if(fileName startsWith "~/")
      IO.succeed(("home", fileName.substring(2)))
    else if(fileName startsWith "$CONFIG/")
      IO.succeed(("config", fileName.substring(8)))
    else
      IO.fail(new RuntimeException("Invalid config path."))

  private def runProxyServer[R <: Blocking with Clock](recorder: ZIORecorder[R], artifact: ZIOArtifactSaver[R]): ZManaged[R, Throwable, Int] =
    for {
      executor <- ZManaged.fromEffect(ZIO.accessM[R] { _.blocking.blockingExecutor })
      runtime <- ZManaged.fromEffect(ZIO.runtime[R])

      server <- {
        val blocker = Blocker.liftExecutionContext(executor.asEC)
        implicit val concurrentEffectInst = zio.interop.catz.taskEffectInstance(runtime)
        implicit val timerInstance = zio.interop.catz.zioTimer[R, Throwable]


        ProxyServer.serverResource[RIO[R, *]](recorder, artifact, blocker).toManaged
      }


    } yield server.address.getPort

  private def runUnixSocketProxy(socketDir: File, port: Int): ZManaged[Blocking, Throwable, Unit] =
    ZManaged.make(
      IO.effect {
        if(SystemUtils.IS_OS_WINDOWS)
          new ProcessBuilder("UnixToLocalhost", s"${socketDir.toString}/helium.sock", port.toString)
            .redirectOutput(ProcessBuilder.Redirect.DISCARD)
            .redirectError(ProcessBuilder.Redirect.DISCARD)
            .start()
        else
          new ProcessBuilder("socat", s"UNIX-LISTEN:${socketDir.toString}/helium.sock,fork,mode=666", s"TCP:localhost:$port")
            .redirectOutput(ProcessBuilder.Redirect.DISCARD)
            .redirectError(ProcessBuilder.Redirect.DISCARD)
            .start()
      }
    ) { process =>
      IO.effectTotal { process.destroy() }
    }
      .unit

}
