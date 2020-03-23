using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mono.Unix;
using Mono.Unix.Native;

namespace Helium.Util
{
    public static class FileUtil
    {
        public static async Task WriteAllTextToDiskAsync(string path, string content, Encoding encoding, CancellationToken cancellationToken) {
            await using var stream = File.Create(path, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var writer = new StreamWriter(stream, encoding);
            await writer.WriteAsync(content.AsMemory(), cancellationToken);
            await writer.FlushAsync();
            stream.Flush(flushToDisk: true);
        }
        
        public static async Task WriteAllBytesToDiskAsync(string path, byte[] content, CancellationToken cancellationToken) {
            await using var stream = File.Create(path, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await stream.WriteAsync(content, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            stream.Flush(flushToDisk: true);
        }

        public static FileStream? CreateNewFile(string path) {
            try {
                return new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
            }
            catch(IOException ex) when ((uint)Marshal.GetHRForException(ex) == 0x80070050) {
                return null;
            }
        }

        public static FileStream CreateTempFile(string parent, out string path, string prefix = "") {
            FileStream? stream;
            do {
                path = Path.Combine(parent, prefix + Path.GetRandomFileName());
                stream = CreateNewFile(path);
            } while(stream == null);

            return stream;
        }
        
        public static void SetUnixMode(string entryFileName, int mode) {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                return;
            }

            new UnixFileInfo(entryFileName).Protection = (FilePermissions)mode;
        }

        public static int? GetUnixMode(string entryFileName) {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                return null;
            }

            return (int)new UnixFileInfo(entryFileName).Protection;
        }

        public static void MakeExecutable(string entryFileName) {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                return;
            }

            new UnixFileInfo(entryFileName).Protection |= FilePermissions.S_IXUSR;
        }
        
        public static void CreateSymlink(string path, string target, bool isDirectory) {
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