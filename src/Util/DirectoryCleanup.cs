using System;
using System.IO;

namespace Helium.Util
{
    public class DirectoryCleanup<T> : ICleanup<T>
    {
        private readonly string dir;

        public DirectoryCleanup(string dir, T value) {
            this.dir = dir;
            Value = value;
        }
        
        public void Dispose() {
            Directory.Delete(dir, recursive: true);
        }

        public T Value { get; }
    }

    public static class DirectoryCleanup
    {
        public static DirectoryCleanup<Func<T>> CreateTempDir<T>(string parent, Func<string, T> value) {
            var name = Path.Combine(parent, Path.GetTempFileName());
            File.Delete(name);
            Directory.CreateDirectory(name);
            return new DirectoryCleanup<Func<T>>(name, () => value(name));
        }
    }
}