using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Tar;
using Mono.Unix;
using Mono.Unix.Native;

namespace Helium.Util
{
    public static class ArchiveUtil
    {
        public static async Task ExtractTar(TarInputStream tarStream, string directory) {
            while(true) {
                var entry = tarStream.GetNextEntry();
                if(entry == null) {
                    break;
                }
                
                EnsurePathDoesNotEscape(directory, entry.Name);
                var entryFileName = Path.Combine(directory, entry.Name);

                if(entry.TarHeader.TypeFlag == TarHeader.LF_LINK) {
                    continue;
                }
                else if(entry.TarHeader.TypeFlag == TarHeader.LF_SYMLINK) {
                    var target = Path.GetDirectoryName(entry.Name) is {} entryDir
                        ? Path.Combine(entryDir, entry.TarHeader.LinkName)
                        : entry.TarHeader.LinkName;
                    
                    EnsurePathDoesNotEscape(directory, target);

                    FileUtil.CreateSymlink(entryFileName, entry.TarHeader.LinkName, entry.IsDirectory);
                }
                else {
                    if(entry.IsDirectory) {
                        Directory.CreateDirectory(entryFileName);
                    }
                    else {
                        if(Path.GetDirectoryName(entryFileName) is {} entryDir) {
                            Directory.CreateDirectory(entryDir);
                        }

                        await using var outStream = File.Create(entryFileName); 
                        tarStream.CopyEntryContents(outStream);
                    }

                    FileUtil.SetUnixMode(entryFileName, entry.TarHeader.Mode);
                }
            }
        }

        public static async Task AddDirToTar(TarOutputStream tarStream, string path, string directory) {
            foreach(var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)) {
                var entryPath = file.Substring(directory.Length);
                if(entryPath.StartsWith(Path.DirectorySeparatorChar) || entryPath.StartsWith(Path.AltDirectorySeparatorChar)) {
                    entryPath = entryPath.Substring(1);
                }

                await AddFileToTar(tarStream, Path.Combine(path, entryPath), file);
            }
        }

        public static async Task AddDirToTar(TarOutputStream tarStream, string path, string directory, Func<string, bool> filter) {
            foreach(var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)) {
                var entryPath = file.Substring(directory.Length);
                
                if(entryPath.StartsWith(Path.DirectorySeparatorChar) || entryPath.StartsWith(Path.AltDirectorySeparatorChar)) {
                    entryPath = entryPath.Substring(1);
                }
                
                if(!filter(entryPath)) {
                    continue;
                }

                await AddFileToTar(tarStream, Path.Combine(path, entryPath), file);
            }
        }

        public static async Task AddFileToTar(TarOutputStream tarStream, string path, string file) {
            var entry = TarEntry.CreateTarEntry(path);
            entry.Size = new FileInfo(file).Length;
            if(FileUtil.GetUnixMode(file) is int mode) entry.TarHeader.Mode = mode;
            tarStream.PutNextEntry(entry);

            await using var fileStream = File.OpenRead(file);
            await fileStream.CopyToAsync(tarStream);
            tarStream.CloseEntry();
        }

        public static async Task AddFileOrDirToTar(TarOutputStream tarStream, string path, string file) {
            if(Directory.Exists(file)) {
                await AddDirToTar(tarStream, path, file);
            }
            else {
                await AddFileToTar(tarStream, path, file);
            }
        }

        public static async Task AddStringToTar(TarOutputStream tarStream, string path, string data) {
            var buffer = Encoding.UTF8.GetBytes(data);
            
            var entry = TarEntry.CreateTarEntry(path);
            entry.Size = buffer.Length;
            tarStream.PutNextEntry(entry);
            await tarStream.WriteAsync(buffer);
            tarStream.CloseEntry();
        }

        private static void EnsurePathDoesNotEscape(string directory, string path) {
            var combined = Path.Combine(directory, path);
            if(!combined.StartsWith(directory)) {
                throw new Exception("Invalid path in archive");
            }
        }
    }
}