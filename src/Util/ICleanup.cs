using System;

namespace Helium.Util
{
    public interface ICleanup<out T> : IAsyncDisposable
    {
        T Value { get; }
    }
}