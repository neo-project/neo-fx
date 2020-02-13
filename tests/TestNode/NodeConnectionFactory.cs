using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoFx.P2P;

namespace NeoFx.TestNode
{
    class NodeConnectionFactory : INodeConnectionFactory
    {
        private readonly IServiceProvider provider;
        private readonly uint magic;

        public NodeConnectionFactory(IServiceProvider provider, IOptions<NetworkOptions> networkOptions)
        {
            this.provider = provider;
            magic = networkOptions.Value.Magic;
        }

        public INodeConnection CreateConnection()
        {
            var pipelineSocket = provider.GetRequiredService<PipelineSocket>();
            var logger = provider.GetService<ILogger<NodeConnection>>();

            return new NodeConnection(pipelineSocket, magic, logger);
        }
    }
}
