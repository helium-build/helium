using System;
using System.IO;
using System.Threading.Tasks;

namespace Helium.Engine.BuildExecutor
{
    internal class ConsoleOutputObserver : IOutputObserver
    {
        private readonly Stream stdout = Console.OpenStandardOutput();
        private readonly Stream stderr = Console.OpenStandardError();

        public Task StandardOutput(byte[] data, int length) =>
            stdout.WriteAsync(data, 0, length);

        public Task StandardError(byte[] data, int length) =>
            stderr.WriteAsync(data, 0, length);
    }
}