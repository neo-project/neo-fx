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
        VersionPayload VersionPayload { get; }
        Task ConnectAsync(string host, int port, uint magic, VersionPayload payload, CancellationToken token = default);
        Task ConnectAsync(IPEndPoint endPoint, uint magic, VersionPayload payload, CancellationToken token = default);
        Task<Message> ReceiveMessage(CancellationToken token);
        ValueTask SendAddrMessage(in AddrPayload payload, CancellationToken token = default);
        ValueTask SendBlockMessage(in BlockPayload payload, CancellationToken token = default);
        ValueTask SendBlockMessage(in Block block, CancellationToken token = default)
        {
            var payload = new BlockPayload(block);
            return SendBlockMessage(payload, token);
        }
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
        ValueTask SendTransactionMessage(in Transaction transaction, CancellationToken token = default)
        {
            var payload = new TransactionPayload(transaction);
            return SendTransactionMessage(payload, token);
        }
    }
}
