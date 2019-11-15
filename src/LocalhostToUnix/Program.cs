using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LocalhostToUnix
{
    class Program
    {
        static int Main(string[] args)
        {
            if(args.Length != 2 || !int.TryParse(args[1], out int port)) {
                Console.WriteLine("Invalid arguments.");
                return 1;
            }

            var unixPath = args[0];

            using(var tcp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)) {
                tcp.Bind(new IPEndPoint(IPAddress.Loopback, port));

                var tokenSource = new CancellationTokenSource();

                Console.CancelKeyPress += (sender, e) => {
                    e.Cancel = !tokenSource.IsCancellationRequested;
                    tcp.Disconnect(reuseSocket: false);
                    tokenSource.Cancel();
                };

                var token = tokenSource.Token;

                while(!token.IsCancellationRequested) {
                    try {
                        tcp.Listen(1);
                        var connSocket = tcp.Accept();
                        Task.Run(async () => await RunServer(connSocket, unixPath));
                    }
                    catch(SocketException) {}
                }
            }

            return 0;
        }

        private static async Task RunServer(Socket tcp, string unixPath) {
            try {
                using(var unix = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)) {
                    await unix.ConnectAsync(new UnixDomainSocketEndPoint(unixPath));

                    try {
                        var t1 = TransferAll(unix, tcp);
                        await TransferAll(tcp, unix);
                        unix.Shutdown(SocketShutdown.Both);
                        await t1;
                    }
                    catch(SocketException ex) {
                        Console.WriteLine(ex);
                    }
                }
            }
            finally {
                tcp.Dispose();
            }
        }

        private static async Task TransferAll(Socket from, Socket to) {
            var buffer = new byte[1024];
            int bytesRead;
            do {
                bytesRead = await from.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None);
                if(bytesRead > 0) {
                    await to.SendAsync(new ArraySegment<byte>(buffer, 0, bytesRead), SocketFlags.None);
                }
            } while(bytesRead > 0);
        }
    }
}
