using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace UnixToLocalhost
{
    class Program
    {
        static int Main(string[] args)
        {
            if(args.Length != 2 || !int.TryParse(args[0], out int port)) {
                Console.WriteLine("Invalid arguments.");
                return 1;
            }

            var unixPath = args[1];

            try {
                using(var unix = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)) {
                    unix.Bind(new UnixDomainSocketEndPoint(unixPath));

                    var tokenSource = new CancellationTokenSource();

                    Console.CancelKeyPress += (sender, e) => {
                        e.Cancel = !tokenSource.IsCancellationRequested;
                        unix.Shutdown(SocketShutdown.Both);
                        tokenSource.Cancel();
                    };

                    var token = tokenSource.Token;

                    while(!token.IsCancellationRequested) {
                        try {
                            unix.Listen(1);
                            var connSocket = unix.Accept();
                            Task.Run(async () => await RunServer(connSocket, port));
                        }
                        catch(SocketException) {}
                    }
                }
            }
            finally {
                try { File.Delete(unixPath); }
                catch { }
            }

            return 0;
        }

        private static async Task RunServer(Socket unix, int port) {
            try {
                using(var tcp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)) {
                    await tcp.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port));

                    try {
                        var t1 = TransferAll(tcp, unix);
                        await TransferAll(unix, tcp);
                        tcp.Shutdown(SocketShutdown.Both);
                        await t1;
                    }
                    catch(SocketException ex) {
                        Console.WriteLine(ex);
                    }
                }
            }
            finally {
                unix.Dispose();
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
