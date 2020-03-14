using Helium.CI.Common.Protocol;
using Thrift.Processor;
using Thrift.Server;
using Thrift.Transport;

namespace Helium.CI.Agent
{
    public class BuildAgentFactory : ITProcessorFactory
    {
        public ITAsyncProcessor GetAsyncProcessor(TTransport? trans, TServer? baseServer = null) =>
            new BuildAgent.AsyncProcessor(new BuildAgentImpl());
        
        
    }
}