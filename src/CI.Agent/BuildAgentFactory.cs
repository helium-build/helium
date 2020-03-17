using Helium.CI.Common.Protocol;
using Helium.Util;
using Thrift.Processor;
using Thrift.Server;
using Thrift.Transport;

namespace Helium.CI.Agent
{
    public class BuildAgentFactory : ITProcessorFactory
    {
        public ITAsyncProcessor GetAsyncProcessor(TTransport trans, TServer? baseServer = null) {
            var buildDir = (TransportBuildDir) trans;
            return new BuildAgent.AsyncProcessor(new BuildAgentImpl(buildDir));
        }
            
        
        
    }
}