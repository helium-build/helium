package dev.helium_build.sdk

import java.util.Locale

sealed trait SdkArch

object SdkArch {
  case object Amd64 extends SdkArch
  case object X86 extends SdkArch
  case object Aarch64 extends SdkArch
  case object Arm extends SdkArch
  case object Ppc64le extends SdkArch
  case object S390x extends SdkArch

  def fromString(str: String): Either[Throwable, SdkArch] =
    str.toLowerCase(Locale.ROOT) match {
      case "x64" | "x86_64" | "amd64" => Right(Amd64)
      case "x86" | "x32" => Right(X86)
      case "arm64" | "aarch64" => Right(Aarch64)
      case "arm" | "arm32" => Right(Arm)
      case "ppc64le" => Right(Ppc64le)
      case "s390x" => Right(S390x)
      case _ => Left(new RuntimeException(s"Unknown architecture $str"))
    }
}
