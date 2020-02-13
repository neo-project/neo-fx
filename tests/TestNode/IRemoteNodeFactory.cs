using System.Threading.Channels;
using NeoFx.P2P.Messages;

namespace NeoFx.TestNode
{
    interface IRemoteNodeFactory
    {
        IRemoteNode CreateRemoteNode(ChannelWriter<(IRemoteNode, Message)> writer);
    }
}
