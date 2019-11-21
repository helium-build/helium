using System;
using System.IO;
using System.Runtime.InteropServices;
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

                    CreateSymlink(entryFileName, entry.TarHeader.LinkName, entry.IsDirectory);
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

                    SetUnixMode(entryFileName, entry.TarHeader.Mode);
                }
            }
        }

        private static void EnsurePathDoesNotEscape(string directory, string path) {
            var combined = Path.Combine(directory, path);
            if(!combined.StartsWith(directory)) {
                throw new Exception("Invalid path in archive");
            }
        }

        public static void SetUnixMode(string entryFileName, int mode) {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                return;
            }

            new UnixFileInfo(entryFileName).Protection = (FilePermissions)mode;
        }

        public static void MakeExecutable(string entryFileName) {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                return;
            }

            new UnixFileInfo(entryFileName).Protection |= FilePermissions.S_IXUSR;
        }
        
        private static void CreateSymlink(string path, string target, bool isDirectory) {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                CreateSymbolicLink(path, target, isDirectory ? SymbolicLinkFlags.Directory : SymbolicLinkFlags.File);
            }
            else {
                new UnixSymbolicLinkInfo(path).CreateSymbolicLinkTo(target);                
            }
        }
        
        
        [DllImport("kernel32.dll")]
        private static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, SymbolicLinkFlags dwFlags);

        private enum SymbolicLinkFlags
        {
            File = 0,
            Directory = 1,
        }
        


    }
}