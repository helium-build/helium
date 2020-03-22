using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        
    }
}