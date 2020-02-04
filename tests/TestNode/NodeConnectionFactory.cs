using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeoFx.P2P;

namespace NeoFx.TestNode
{
    class NodeConnectionFactory : INodeConnectionFactory
    {
        private readonly IServiceProvider provider;
        private readonly ILogger<NodeConnectionFactory> log;

        public NodeConnectionFactory(IServiceProvider provider, ILogger<NodeConnectionFactory> logger)
        {
            this.provider = provider;
            log = logger;
        }

        public INodeConnection CreateConnection()
        {
            return provider.GetRequiredService<INodeConnection>();
        }
    }
}
