using System.Diagnostics;
using System.Threading.Tasks;

namespace Helium.Util
{
    public static class ProcessUtil
    {
        public static Task WaitForExitAsync(this Process process) {
            var tcs = new TaskCompletionSource<object?>();

            process.EnableRaisingEvents = true;
            process.Exited += delegate { tcs.TrySetResult(null); };

            return tcs.Task;
        }
    }
}