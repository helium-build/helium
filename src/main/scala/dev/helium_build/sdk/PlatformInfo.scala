package dev.helium_build.sdk

import org.apache.commons.lang3.arch.Processor
import org.apache.commons.lang3.{ArchUtils, SystemUtils}
import zio._

final case class PlatformInfo
(
  os: SdkOperatingSystem,
  arch: SdkArch,
)

object PlatformInfo {

  def current: Task[PlatformInfo] = for {
    os <- IO.effect {
      if(SystemUtils.IS_OS_WINDOWS)
        SdkOperatingSystem.Windows
      else if(SystemUtils.IS_OS_LINUX)
        SdkOperatingSystem.Linux
      else
        throw new RuntimeException("Unsupported OS")
    }
    arch <- IO.effect {
      val processor = ArchUtils.getProcessor
      (processor.getArch, processor.getType) match {
        case (Processor.Arch.BIT_32, Processor.Type.X86) => SdkArch.X86
        case (Processor.Arch.BIT_64, Processor.Type.X86) => SdkArch.Amd64
        case (Processor.Arch.BIT_64, Processor.Type.PPC) => SdkArch.Ppc64le
        case (_, _) => throw new RuntimeException("Unsupported Architecture")
      }
    }
  } yield PlatformInfo(os, arch)

}
