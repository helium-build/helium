using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Helium.Util
{
    public static class FileUtil
    {
        public static async Task WriteAllTextToDiskAsync(string path, string content, Encoding encoding, CancellationToken cancellationToken) {
            await using var stream = File.Create(path, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using(var writer = new StreamWriter(stream, encoding)) {
                await writer.WriteAsync(content.AsMemory(), cancellationToken);
                await writer.FlushAsync();
            }
            stream.Flush(flushToDisk: true);
        }
    }
}