package dev.helium_build.record

import java.io.File

import cats.effect.Resource
import dev.helium_build.sdk.{SdkInfo, SdkInstallManager}
import zio.{ZIO, ZManaged}
import zio.interop.catz._
import zio.stream.ZStream

trait ZIORecorder[R] extends Recorder[ZIO[R, Throwable, *]] {

  def sdkInstallManager: ZIO[R, Throwable, SdkInstallManager]
  def availableSdks: ZStream[R, Throwable, SdkInfo]

}
