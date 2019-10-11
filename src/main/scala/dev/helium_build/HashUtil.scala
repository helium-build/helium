package dev.helium_build

import java.nio.charset.StandardCharsets
import java.security.MessageDigest

import org.apache.commons.codec.binary.Hex

object HashUtil {

  private def formatHash(a: Array[Byte]): String =
    Hex.encodeHexString(a)

  def sha256UTF8(str: String): String =
    formatHash(MessageDigest.getInstance("SHA-256").digest(str.getBytes(StandardCharsets.UTF_8)))


}
