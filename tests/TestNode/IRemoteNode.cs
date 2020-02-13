using System.Threading;
using NeoFx.P2P.Messages;
using System.Net;

namespace NeoFx.TestNode
{
    interface IRemoteNode
    {
        VersionPayload VersionPayload { get; }
        void Connect(string address, int port, in VersionPayload version, CancellationToken token = default);
        void Connect(IPEndPoint endPoint, in VersionPayload version, CancellationToken token = default);
    }
}
