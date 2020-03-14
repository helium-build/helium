using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Helium.CI.Common.Protocol;
using Helium.Pipeline;
using Helium.Sdks;
using Newtonsoft.Json;
using Nito.AsyncEx;
using Thrift.Protocol;
using Thrift.Transport;

namespace Helium.CI.Server
{
    public abstract class AgentManager
    {
        private const int MaxWorkers = 100;
        
        private readonly IJobQueue jobQueue;

        private volatile int workers = 1;
        private readonly AsyncSemaphore semaphore = new AsyncSemaphore(1);
        private readonly ConcurrentDictionary<IRunnableJob, Task> workerTasks = new ConcurrentDictionary<IRunnableJob, Task>();

        public AgentManager(IJobQueue jobQueue) {
            this.jobQueue = jobQueue;
        }

        public int Workers => workers;

        protected abstract TTransport CreateTransport();
        
        protected virtual TProtocol CreateProtocol(TTransport transport) =>
            new TBinaryProtocol(transport);
        
        public Task UpdateWorkerCount(int workers) {
            if(workers > MaxWorkers) throw new ArgumentOutOfRangeException();
            throw new NotImplementedException();
        }

        public async Task AcceptJobs(CancellationToken cancellationToken) {
            try {
                while(!cancellationToken.IsCancellationRequested) {
                    await semaphore.WaitAsync(cancellationToken);
                    try {
                        using var transport = CreateTransport();
                        using var protocol = CreateProtocol(transport);
                        using var client = new BuildAgent.Client(protocol);

                        await transport.OpenAsync(cancellationToken);

                        await client.supportsPlatformAsync(JsonConvert.SerializeObject(PlatformInfo.Current), cancellationToken);
                        
                        var job = await jobQueue.AcceptJob(task => JobFilter(client, task, cancellationToken), cancellationToken);
                        CompleteJob(client, job, cancellationToken);
                    }
                    catch {
                        semaphore.Release();
                        throw;
                    }
                }
            }
            catch(OperationCanceledException) {
                await Task.WhenAll(workerTasks.Values.ToArray());
                throw;
            }
        }

        private void CompleteJob(BuildAgent.Client agent, IRunnableJob job, CancellationToken cancellationToken) {
            var task = Task.Run(() => job.Run(agent, cancellationToken), cancellationToken);
            workerTasks.TryAdd(job, task);
            Task.Run(async () => {
                try {
                    await task;
                }
                finally {
                    semaphore.Release();
                    workerTasks.TryRemove(job, out _);
                }
            });
        }

        private async Task<bool> JobFilter(BuildAgent.Client agent, BuildTask arg, CancellationToken cancellationToken) =>
            await agent.supportsPlatformAsync(JsonConvert.ToString(arg.Platform), cancellationToken);
    }
}