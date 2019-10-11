package dev.helium_build.record
import java.io.File
import java.nio.charset.StandardCharsets

import dev.helium_build.build.BuildSchema
import dev.helium_build.sdk.{SdkInfo, SdkInstallManager, SdkLoader}
import org.apache.commons.io.FileUtils
import zio.{IO, ZIO}
import zio.blocking.Blocking
import zio.stream.ZStream

abstract class LiveRecorder[R <: Blocking](sdkDir: File, override val workDir: File) extends ZIORecorder[R]  {

  override def availableSdks: ZStream[R, Throwable, SdkInfo] =
    SdkLoader.loadSdks(sdkDir)

}
