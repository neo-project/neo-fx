using NeoFx.P2P;

namespace NeoFx.TestNode
{
    interface INodeConnectionFactory
    {
        INodeConnection CreateConnection(uint magic);
    }
}
