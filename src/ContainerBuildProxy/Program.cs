using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Helium.Env;


namespace ContainerBuildProxy
{
    public static class Program
    {
        public static async Task Main(string[] args) {
            var proxy = new BuildProxy("/http-cache");
            proxy.Start();
            
            var exitTcs = new TaskCompletionSource<object?>();

            Console.CancelKeyPress += (sender, e) => {
                if(exitTcs.TrySetResult(null)) {
                    e.Cancel = true;
                }
            };

            await exitTcs.Task;

            proxy.Stop();
        }

    }
}
