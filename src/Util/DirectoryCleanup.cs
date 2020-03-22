using System;
using System.IO;
using System.Threading.Tasks;

namespace Helium.Util
{
    public class DirectoryCleanup<T> : ICleanup<T>
    {
        private readonly string dir;

        public DirectoryCleanup(string dir, T value) {
            this.dir = dir;
            Value = value;
        }
        
        public async ValueTask DisposeAsync() {
            Dispose();
        }

        public void Dispose() {
            try { Directory.Delete(dir, recursive: true); }
            catch {}
        }

        public T Value { get; }
    }

    public static class DirectoryCleanup
    {
        public static DirectoryCleanup<Func<T>> CreateTempDir<T>(string parent, Func<string, T> value, string prefix = "") {
            var name = DirectoryUtil.CreateTempDirectory(parent, prefix);
            Directory.CreateDirectory(name);
            return new DirectoryCleanup<Func<T>>(name, () => value(name));
        }
        
        public static DirectoryCleanup<string> CreateTempDir(string parent, string prefix = "") {
            var name = DirectoryUtil.CreateTempDirectory(parent, prefix);
            Directory.CreateDirectory(name);
            return new DirectoryCleanup<string>(name, name);
        }
    }
}