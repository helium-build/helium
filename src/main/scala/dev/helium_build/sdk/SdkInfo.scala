package dev.helium_build.sdk

import dev.helium_build.build.RequiredSdk
import com.github.zafarkhaja.semver.Version

final case class SdkInfo
(
  implements: Seq[String],
  version: String,
  os: Option[SdkOperatingSystem],
  architecture: Option[SdkArch],
  setupSteps: Seq[SdkSetupStep],
  pathDirs: Seq[String],
  env: Map[String, EnvValue],
  configFileTemplates: Option[Map[String, String]] = None,
) {

  def matches(requiredSdk: RequiredSdk): Boolean =
    implements.contains(requiredSdk.name) && {
      try {
        val reqVer = Version.valueOf(requiredSdk.minVersion)
        val currVer = Version.valueOf(version)
        currVer.greaterThanOrEqualTo(reqVer)
      }
      catch {
        case _: Exception => false
      }
    }

  def matchesPlatform(platformInfo: PlatformInfo): Boolean =
    architecture.forall { _ == platformInfo.arch } && os.forall { _ == platformInfo.os }

}
