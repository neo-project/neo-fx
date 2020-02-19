using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using NeoFx.P2P.Messages;
using System;

namespace NeoFx.TestNode
{
    class RemoteNodeFactory : IRemoteNodeFactory
    {
        private readonly IServiceProvider provider;

        public RemoteNodeFactory(IServiceProvider provider)
        {
            this.provider = provider;
        }

        public IRemoteNode CreateRemoteNode(ChannelWriter<Message> writer)
        {
            var connectionFactory = provider.GetRequiredService<INodeConnectionFactory>();
            var logger = provider.GetService<ILogger<RemoteNode>>();

            return new RemoteNode(connectionFactory.CreateConnection(), writer, logger);
        }
    }
}
