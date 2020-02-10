using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NeoFx.Models;
using NeoFx.P2P.Messages;

namespace NeoFx.P2P
{
    public interface INodeConnection
    {
        Task<VersionPayload> ConnectAsync(string host, int port, uint magic, VersionPayload payload, CancellationToken token = default);
        Task<VersionPayload> ConnectAsync(IPEndPoint endPoint, uint magic, VersionPayload payload, CancellationToken token = default);
        ValueTask<Message> ReceiveMessage(uint magic, CancellationToken token);
        ValueTask SendAddrMessage(uint magic, in AddrPayload payload, CancellationToken token = default);
        ValueTask SendBlockMessage(uint magic, in BlockPayload payload, CancellationToken token = default);
        ValueTask SendConsensusMessage(uint magic, in ConsensusPayload payload, CancellationToken token = default);
        ValueTask SendGetAddrMessage(uint magic, CancellationToken token = default);
        ValueTask SendGetBlocksMessage(uint magic, in HashListPayload payload, CancellationToken token = default);
        ValueTask SendGetDataMessage(uint magic, in InventoryPayload payload, CancellationToken token = default);
        ValueTask SendGetHeadersMessage(uint magic, in HashListPayload payload, CancellationToken token = default);
        ValueTask SendHeadersMessage(uint magic, in HeadersPayload payload, CancellationToken token = default);
        ValueTask SendInvMessage(uint magic, in InventoryPayload payload, CancellationToken token = default);
        ValueTask SendPingMessage(uint magic, in PingPongPayload payload, CancellationToken token = default);
        ValueTask SendPongMessage(uint magic, in PingPongPayload payload, CancellationToken token = default);
        ValueTask SendTransactionMessage(uint magic, in TransactionPayload payload, CancellationToken token = default);
    }
}
