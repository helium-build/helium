package dev.helium_build.sdk

sealed trait SdkHash
object SdkHash {
  final case class Sha256(value: String) extends SdkHash
  final case class Sha512(value: String) extends SdkHash
}
