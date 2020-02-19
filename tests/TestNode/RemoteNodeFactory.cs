using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using NeoFx.P2P.Messages;
using System;

namespace NeoFx.TestNode
{
    interface IRemoteNodeFactory
    {
        IRemoteNode CreateRemoteNode();
    }

    class RemoteNodeFactory : IRemoteNodeFactory
    {
        private readonly IServiceProvider provider;

        public RemoteNodeFactory(IServiceProvider provider)
        {
            this.provider = provider;
        }

        public IRemoteNode CreateRemoteNode()
        {
            return provider.GetRequiredService<IRemoteNode>();
        }
    }
}
