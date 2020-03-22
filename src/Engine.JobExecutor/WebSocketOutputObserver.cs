using System;
using System.Threading.Tasks;
using Fleck;

namespace Helium.Engine.JobExecutor
{
    internal class WebSocketOutputObserver : IOutputObserver
    {
        public WebSocketOutputObserver(IWebSocketConnection conn) {
            this.conn = conn;
        }
     
        private readonly IWebSocketConnection conn;
   
        public async Task StandardOutput(byte[] data, int length) {
            var b2 = new byte[1 + length];
            b2[0] = 0;
            Array.Copy(data, 0, b2, 1, length);
            await conn.Send(b2);
        }

        public async Task StandardError(byte[] data, int length) {
            var b2 = new byte[1 + length];
            b2[0] = 1;
            Array.Copy(data, 0, b2, 1, length);
            await conn.Send(b2);
        }
    }
}