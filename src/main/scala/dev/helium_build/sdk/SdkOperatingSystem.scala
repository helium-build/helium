package dev.helium_build.sdk

import java.util.Locale

sealed trait SdkOperatingSystem
object SdkOperatingSystem {
  case object Linux extends SdkOperatingSystem
  case object Windows extends SdkOperatingSystem


  def fromString(str: String): Either[Throwable, SdkOperatingSystem] =
    str.toLowerCase(Locale.ROOT) match {
      case "linux" => Right(Linux)
      case "windows" | "win" | "win32" => Right(Windows)
      case _ => Left(new RuntimeException(s"Unknown operating system $str"))
    }
}
