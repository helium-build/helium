using System.Text;
using System.Threading.Tasks;

namespace Helium.Engine.BuildExecutor
{
    internal interface IOutputObserver
    {
        Task StandardOutput(byte[] data, int length);
        Task StandardError(byte[] data, int length);

        Task StandardOutput(string data) {
            var buff = Encoding.UTF8.GetBytes(data);
            return StandardOutput(buff, buff.Length);
        }
    }
}