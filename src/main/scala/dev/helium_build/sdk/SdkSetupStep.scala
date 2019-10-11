package dev.helium_build.sdk

sealed trait SdkSetupStep

final case class SdkDownload(url: String, fileName: String, hash: SdkHash) extends SdkSetupStep
final case class SdkExtract(fileName: String, directory: String) extends SdkSetupStep
final case class SdkDelete(fileName: String) extends SdkSetupStep
final case class SdkCreateDirectory(fileName: String) extends SdkSetupStep
final case class SdkCreateFile(fileName: String, isExecutable: Boolean, content: String) extends SdkSetupStep
