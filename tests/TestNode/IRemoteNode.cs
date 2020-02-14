using System.Threading;
using NeoFx.P2P.Messages;
using System.Net;
using System.Threading.Tasks;

namespace NeoFx.TestNode
{
    interface IRemoteNode
    {
        VersionPayload VersionPayload { get; }
        EndPoint RemoteEndPoint { get; }
        Task Connect(IPEndPoint endPoint, VersionPayload payload, CancellationToken token = default);
        ValueTask SendAddrMessage(in AddrPayload payload, CancellationToken token = default);
        ValueTask SendBlockMessage(in BlockPayload payload, CancellationToken token = default);
        ValueTask SendConsensusMessage(in ConsensusPayload payload, CancellationToken token = default);
        ValueTask SendGetAddrMessage(CancellationToken token = default);
        ValueTask SendGetBlocksMessage(in HashListPayload payload, CancellationToken token = default);
        ValueTask SendGetDataMessage(in InventoryPayload payload, CancellationToken token = default);
        ValueTask SendGetHeadersMessage(in HashListPayload payload, CancellationToken token = default);
        ValueTask SendHeadersMessage(in HeadersPayload payload, CancellationToken token = default);
        ValueTask SendInvMessage(in InventoryPayload payload, CancellationToken token = default);
        ValueTask SendPingMessage(in PingPongPayload payload, CancellationToken token = default);
        ValueTask SendPongMessage(in PingPongPayload payload, CancellationToken token = default);
        ValueTask SendTransactionMessage(in TransactionPayload payload, CancellationToken token = default);
    }
}
