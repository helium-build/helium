using System.Threading.Tasks;

namespace Helium.Engine.JobExecutor
{
    internal interface IOutputObserver
    {
        Task StandardOutput(byte[] data, int length);
        Task StandardError(byte[] data, int length);
    }
}