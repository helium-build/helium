package dev.helium_build.util

import java.io.{BufferedInputStream, File, FileInputStream, FileOutputStream}
import java.nio.file.attribute.PosixFilePermission
import java.nio.file.{Files, Path}

import org.apache.commons.compress.archivers.tar.{TarArchiveEntry, TarArchiveInputStream, TarArchiveOutputStream, TarConstants}
import org.apache.commons.compress.archivers.zip.ZipArchiveInputStream
import org.apache.commons.compress.compressors.gzip.GzipCompressorInputStream
import org.apache.commons.io.{FilenameUtils, IOUtils}
import zio.{IO, Task, RIO, ZIO, ZManaged}
import zio.blocking.Blocking
import cats.implicits._
import zio.interop.catz._

object ArchiveUtil {


  def extractArchive(archive: File, dir: File): RIO[Blocking, Unit] =
    ZManaged.fromAutoCloseable(ZIO.accessM[Blocking] { _.blocking.effectBlocking {new FileInputStream(archive) } })
      .flatMap { stream =>
        ZManaged.fromAutoCloseable(ZIO.accessM[Blocking] { _.blocking.effectBlocking {new BufferedInputStream(stream) } })
      }
      .flatMap { stream =>
        val fileName = archive.toString
        if(fileName.endsWith(".tar.gz") || FilenameUtils.isExtension(fileName, "tgz"))
          ZManaged.fromAutoCloseable(ZIO.accessM[Blocking] { _.blocking.effectBlocking {new GzipCompressorInputStream(stream) } })
            .flatMap { ungzStream =>
              ZManaged.fromAutoCloseable(ZIO.accessM[Blocking] { _.blocking.effectBlocking {new TarArchiveInputStream(ungzStream) } })
            }
        else if (FilenameUtils.isExtension(fileName, "tar"))
          ZManaged.fromAutoCloseable(ZIO.accessM[Blocking] { _.blocking.effectBlocking {new TarArchiveInputStream(stream) } })
        else if(FilenameUtils.isExtension(fileName, "zip"))
          ZManaged.fromAutoCloseable(ZIO.accessM[Blocking] { _.blocking.effectBlocking {new ZipArchiveInputStream(stream) } })
        else
          ZManaged.fail(new RuntimeException(s"Unknown archive type for $fileName"))
      }
      .use { stream =>

        def extractRemaining(): RIO[Blocking, Unit] =
          ZIO.accessM[Blocking] { _.blocking.effectBlocking { stream.getNextEntry } }
            .flatMap { entry =>
              if(entry == null)
                IO.succeed(())
              else
                ZIO.accessM[Blocking] { _.blocking.effectBlocking { stream.canReadEntryData(entry) } }.flatMap {
                  case false => IO.fail(new RuntimeException(s"Could not read data for entry ${entry.getName}"))
                  case true =>
                    normalizePath(entry.getName)
                      .flatMap { entryName =>
                        IO.effectTotal { new File(dir, entryName) }
                          .flatMap { entryFile =>
                            (
                              entry match {
                                case entry: TarArchiveEntry if entry.isSymbolicLink =>
                                  // Normalize path to verify that the symlink target is within the tar file
                                  if(entry.getLinkName.startsWith("/"))
                                    IO.fail(new RuntimeException("Symlink path should not be absolute."))
                                  else
                                    normalizePath(entry.getName + "/../" + entry.getLinkName).flatMap { _ =>
                                      ZIO.accessM[Blocking] { _.blocking.effectBlocking {
                                        Files.createSymbolicLink(entryFile.toPath, Path.of(entry.getLinkName))
                                      } }
                                    }

                                case _ if entry.isDirectory =>
                                  ZIO.accessM[Blocking] { _.blocking.effectBlocking { entryFile.mkdirs() } }

                                case _ =>
                                  ZIO.accessM[Blocking] { _.blocking.effectBlocking { entryFile.getParentFile.mkdirs() } }.flatMap { _ =>
                                    ZManaged.fromAutoCloseable(ZIO.accessM[Blocking] { _.blocking.effectBlocking { new FileOutputStream(new File(dir, entryName)) } })
                                      .use { outStream =>
                                        ZIO.accessM[Blocking] { _.blocking.effectBlocking { IOUtils.copy(stream, outStream) } }
                                      }
                                  }
                              }
                              )
                              .flatMap { _ =>
                                entry match {
                                  case entry: TarArchiveEntry if !entry.isSymbolicLink =>
                                    ZIO.accessM[Blocking] { _.blocking.effectBlocking {
                                      Files.setPosixFilePermissions(entryFile.toPath, getPosixPermissions(entry.getMode))
                                    } }
                                  case _ => IO.succeed(())
                                }
                              }
                          }
                      }
                      .flatMap { _ => extractRemaining() }
                }
            }


        extractRemaining()
      }


  def normalizePath(fileName: String): Task[String] = {
    val normalizedFile = FilenameUtils.normalize(fileName)

    val fail = IO.fail(new RuntimeException(s"Invalid path: $fileName"))

    if(normalizedFile == null)
      fail
    else
      IO.effectTotal { new File(normalizedFile).isAbsolute }.flatMap {
        case true => fail
        case false => IO.succeed(normalizedFile)
      }
  }

  private def getPosixPermissions(mode: Int): java.util.Set[PosixFilePermission] = {
    val set = new java.util.HashSet[PosixFilePermission]()

    if((mode & 4) == 4) set.add(PosixFilePermission.OTHERS_READ)
    if((mode & 2) == 2) set.add(PosixFilePermission.OTHERS_WRITE)
    if((mode & 1) == 1) set.add(PosixFilePermission.OTHERS_EXECUTE)
    if(((mode >>> 3) & 4) == 4) set.add(PosixFilePermission.GROUP_READ)
    if(((mode >>> 3) & 2) == 2) set.add(PosixFilePermission.GROUP_WRITE)
    if(((mode >>> 3) & 1) == 1) set.add(PosixFilePermission.GROUP_EXECUTE)
    if(((mode >>> 6) & 4) == 4) set.add(PosixFilePermission.OWNER_READ)
    if(((mode >>> 6) & 2) == 2) set.add(PosixFilePermission.OWNER_WRITE)
    if(((mode >>> 6) & 1) == 1) set.add(PosixFilePermission.OWNER_EXECUTE)

    set
  }



  private def getPosixPermissionsMode(set: java.util.Set[PosixFilePermission]): Int = {
    var mode = 0

    if(set.contains(PosixFilePermission.OTHERS_READ)) mode |= 4
    if(set.contains(PosixFilePermission.OTHERS_WRITE)) mode |= 2
    if(set.contains(PosixFilePermission.OTHERS_EXECUTE)) mode |= 1
    if(set.contains(PosixFilePermission.GROUP_READ)) mode |= (4 << 3)
    if(set.contains(PosixFilePermission.GROUP_WRITE)) mode |= (2 << 3)
    if(set.contains(PosixFilePermission.GROUP_EXECUTE)) mode |= (1 << 3)
    if(set.contains(PosixFilePermission.OWNER_READ)) mode |= (4 << 6)
    if(set.contains(PosixFilePermission.OWNER_WRITE)) mode |= (2 << 6)
    if(set.contains(PosixFilePermission.OWNER_EXECUTE)) mode |= (1 << 6)

    mode
  }
    

  private def setEntryPermissions(entry: TarArchiveEntry, file: File): Unit = {
    try {
      val set = Files.getPosixFilePermissions(file.toPath)
      entry.setMode(getPosixPermissionsMode(set))
    }
    catch {
      case _: UnsupportedOperationException =>
    }
  }



  def addFileToArchive(archive: TarArchiveOutputStream, path: String, file: File): ZIO[Blocking, Throwable, Unit] =
    ZIO.accessM { _.blocking.effectBlocking {
      if(Files.isSymbolicLink(file.toPath)) {
        val entry = new TarArchiveEntry(path, TarConstants.LF_SYMLINK)
        entry.setLinkName(Files.readSymbolicLink(file.toPath).toString)
        archive.putArchiveEntry(entry)
        archive.closeArchiveEntry()
      }
      else {
        val entry = new TarArchiveEntry(file, path)
        setEntryPermissions(entry, file)
        archive.putArchiveEntry(entry)
        val fileStream = new FileInputStream(file)
        try IOUtils.copy(fileStream, archive)
        finally fileStream.close()
        archive.closeArchiveEntry()
      }
    } }

  def addDirToArchive(archive: TarArchiveOutputStream, path: String, dir: File): ZIO[Blocking, Throwable, Unit] =
    ZIO.accessM[Blocking] { _.blocking.effectBlocking { Files.isSymbolicLink(dir.toPath) }}
      .flatMap {
        case true => addFileToArchive(archive, path, dir)
        case false =>
          ZIO.accessM[Blocking] { _.blocking.effectBlocking {
            val entry = new TarArchiveEntry(dir, path)
            setEntryPermissions(entry, dir)
            archive.putArchiveEntry(entry)
            archive.closeArchiveEntry()

            dir.listFiles()
          }}
            .flatMap { files =>
              files.toVector.traverse_ { file =>
                ZIO.accessM[Blocking] { _.blocking.effectBlocking { file.isDirectory }}.flatMap {
                  case true => addDirToArchive(archive, path + "/" + file.getName, file)
                  case false => addFileToArchive(archive, path + "/" + file.getName, file)
                }
              }
            }
      }

}
