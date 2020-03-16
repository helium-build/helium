using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Helium.CI.Common.Protocol;
using Helium.Pipeline;
using Helium.Sdks;
using Newtonsoft.Json;
using Nito.AsyncEx;
using Thrift.Protocol;
using Thrift.Transport;
using Thrift.Transport.Client;

namespace Helium.CI.Server
{
    internal sealed class AgentExecutor
    {
        public AgentExecutor(IJobQueue jobQueue, AgentConfig config, ServerConfig serverConfig) {
            this.jobQueue = jobQueue;
            this.config = config;
            this.cert = serverConfig.Cert;
        }
        
        
        private const int MaxWorkers = 100;
        
        private int runningJobs = 0;
        private readonly IJobQueue jobQueue;
        private AgentConfig config;
        private readonly X509Certificate2 cert;

        private readonly AsyncMonitor workerUpdate = new AsyncMonitor();
        private readonly ConcurrentDictionary<IRunnableJob, Task> workerTasks = new ConcurrentDictionary<IRunnableJob, Task>();
        private volatile bool stop = false;
        
        public AgentConfig Config => config;

        private TTransport CreateTransport(AgentConnection conn) => conn switch {
            SslAgentConnection ssl => new TTlsSocketTransport(ssl.Host, ssl.Port, 0, cert, certValidator: CertValidator(ssl.AgentCert)),
            _ => throw new Exception("Unexpected agent config type")
        };
        
        private static RemoteCertificateValidationCallback CertValidator(X509Certificate2 agentCert) => (sender, certificate, chain, policyErrors) =>
            certificate.Equals(agentCert);

        public void Stop() {
            stop = true;
        }

        public async Task UpdateConfig(AgentConfig config, CancellationToken cancellationToken) {
            if(config.Workers > MaxWorkers || config.Workers < 1) throw new ArgumentOutOfRangeException();
            using(await workerUpdate.EnterAsync(cancellationToken)) {
                if(config.Workers > this.config.Workers) {
                    this.config = config;
                    workerUpdate.PulseAll();
                }
                else {
                    this.config = config;
                }
            }
        }

        public async Task AcceptJobs(CancellationToken cancellationToken) {
            try {
                while(!stop && !cancellationToken.IsCancellationRequested) {
                    AgentConfig config;
                    using(await workerUpdate.EnterAsync(cancellationToken)) {
                        config = this.config;
                        while(runningJobs >= config.Workers) {
                            await workerUpdate.WaitAsync(cancellationToken);
                            config = this.config;
                        }

                        ++runningJobs;
                    }
                    
                    using var transport = CreateTransport(config.Connection);
                    using var protocol = new TBinaryProtocol(transport);
                    using var client = new BuildAgent.Client(protocol);

                    await client.supportsPlatformAsync(JsonConvert.SerializeObject(PlatformInfo.Current), cancellationToken);
                        
                    var job = await jobQueue.AcceptJob(task => JobFilter(client, task, cancellationToken), cancellationToken);
                    Interlocked.Increment(ref runningJobs);
                        
                    CompleteJob(client, job, cancellationToken);
                }
            }
            catch(OperationCanceledException) {
                await Task.WhenAll(workerTasks.Values.ToArray());
                throw;
            }
            await Task.WhenAll(workerTasks.Values.ToArray());
        }

        private void CompleteJob(BuildAgent.Client agent, IRunnableJob job, CancellationToken cancellationToken) {
            var task = Task.Run(() => job.Run(agent, cancellationToken), cancellationToken);
            workerTasks.TryAdd(job, task);
            Task.Run(async () => {
                try {
                    await task;
                }
                finally {
                    using(await workerUpdate.EnterAsync(cancellationToken)) {
                        --runningJobs;
                        workerUpdate.PulseAll();
                    }
                    workerTasks.TryRemove(job, out _);
                }
            }, cancellationToken);
        }

        private async Task<bool> JobFilter(BuildAgent.Client agent, BuildTask arg, CancellationToken cancellationToken) =>
            await agent.supportsPlatformAsync(JsonConvert.ToString(arg.Platform), cancellationToken);
    }
}