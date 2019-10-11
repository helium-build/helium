package dev.helium_build.sdk

sealed trait EnvValue
object EnvValue {
  final case class OfString(value: String) extends EnvValue
  final case class Concat(values: Seq[EnvValue]) extends EnvValue
  final case object SdkDirectory extends EnvValue
}
