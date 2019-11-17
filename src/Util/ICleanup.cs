using System;

namespace Helium.Util
{
    public interface ICleanup<out T> : IDisposable
    {
        T Value { get; }
    }
}