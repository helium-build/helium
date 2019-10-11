package dev.helium_build.sdk

import java.io.{File, FileInputStream, InputStreamReader}
import java.nio.charset.StandardCharsets
import java.nio.file.Files

import dev.helium_build.HashUtil
import org.apache.commons.io.{FileUtils, FilenameUtils}
import zio.{IO, UIO, ZIO, ZManaged}
import zio.blocking.Blocking
import zio.stream._
import io.circe
import io.circe.syntax._
import io.circe.generic.auto._

import scala.jdk.StreamConverters._

object SdkLoader {

  def loadSdk(sdkFile: File): ZIO[Blocking, circe.Error, SdkInfo] =
    readFile(sdkFile).flatMap { text => IO.fromEither(circe.parser.decode[SdkInfo](text)) }

  def loadSdks(dir: File): ZStream[Blocking, circe.Error, SdkInfo] =
    findFiles(dir).mapMPar(100)(loadSdk)

  private def findFiles(dir: File): ZStream[Blocking, Nothing, File] =
    ZStream.fromEffect {
      ZIO.environment[Blocking].flatMap {
        _.blocking.effectBlocking {
          ZStream.fromIterable(
            Files.walk(dir.toPath)
              .toScala(LazyList)
          )
        }.orDie
      }
    }
      .flatMap(identity)
      .map { path => path.toFile }
      .filterM { file => ZIO.accessM[Blocking] { _.blocking.effectBlocking { !file.isDirectory && FilenameUtils.isExtension(file.toString, "json") }.orDie } }



  private def readFile(file: File): ZIO[Blocking, Nothing, String] =
    ZIO.environment[Blocking].flatMap { _.blocking.effectBlocking { FileUtils.readFileToString(file, StandardCharsets.UTF_8) }.orDie }


  def saveSdk(sdk: SdkInfo, file: File): ZIO[Blocking, Nothing, Unit] =
    ZIO.environment[Blocking].flatMap { _.blocking.effectBlocking { FileUtils.writeStringToFile(file, sdk.asJson.spaces2, StandardCharsets.UTF_8) }.orDie }

  def sdkSha256(sdk: SdkInfo): String =
    HashUtil.sha256UTF8(sdk.asJson.noSpacesSortKeys)


}
