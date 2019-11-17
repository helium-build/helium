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
            Directory.Delete(dir, recursive: true);
        }

        public T Value { get; }
    }

    public static class DirectoryCleanup
    {
        public static DirectoryCleanup<Func<T>> CreateTempDir<T>(string parent, Func<string, T> value) {
            var name = Path.Combine(parent, Path.GetRandomFileName());
            Directory.CreateDirectory(name);
            return new DirectoryCleanup<Func<T>>(name, () => value(name));
        }
    }
}