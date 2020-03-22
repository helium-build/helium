using System;

namespace Helium.Util
{
    public interface ICleanup<out T> : IAsyncDisposable, IDisposable
    {
        T Value { get; }
    }
}