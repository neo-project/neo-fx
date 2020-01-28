using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using DevHawk.Buffers;
using Microsoft.Extensions.Logging;

namespace NeoFx.P2P.Messages
{
    public abstract class Message
    {
        public readonly MessageHeader Header;

        public uint Magic => Header.Magic;
        public string Command => Header.Command;
        public uint Length => Header.Length;
        public uint Checksum => Header.Checksum;

        protected Message(in MessageHeader header)
        {
            Header = header;
        }

        abstract public void LogMessage(ILogger logger);

        public static bool TryRead(ReadOnlySequence<byte> sequence, in MessageHeader header, [MaybeNullWhen(false)] out Message message)
        {
            if (header.Length == 0)
            {
                switch (header.Command)
                {
                    case VerAckMessage.CommandText:
                        message = new VerAckMessage(header);
                        return true;
                    case GetAddrMessage.CommandText:
                        message = new GetAddrMessage(header);
                        return true;
                }
            }
            else
            {
                var reader = new BufferReader<byte>(sequence);
                switch (header.Command)
                {
                    case VersionMessage.CommandText:
                        {
                            if (VersionMessage.TryRead(ref reader, header, out var _message))
                            {
                                message = _message;
                                return true;
                            }
                        }
                        break;
                    //     return TryReadVersionMsg(ref reader, header, out message);
                    // case GetBlocksMessage.CommandText:
                    //     return TryReadGetBlocksMsg(ref reader, header, out message);
                    // case GetHeadersMessage.CommandText:
                    //     return TryReadGetHeadersMsg(ref reader, header, out message);
                    // case InvMessage.CommandText:
                    //     return TryReadInvMsg(ref reader, header, out message);
                    // case GetDataMessage.CommandText:
                    //     return TryReadGetDataMsg(ref reader, header, out message);
                    // case AddrMessage.CommandText:
                    //     return TryReadAddrMsg(ref reader, header, out message);
                    // case BlockMessage.CommandText:
                    //     return TryReadBlockMsg(ref reader, header, out message);
                    // case ConsensusMessage.CommandText:
                    //     return TryReadConsensusMsg(ref reader, header, out message);
                    // case HeadersMessage.CommandText:
                    //     return TryReadHeadersMsg(ref reader, header, out message);
                    // case TransactionMessage.CommandText:
                    //     return TryReadTransactionMessage(ref reader, header, out message);
                        // filteradd, filterclear, filterload
                        // mempool
                }
            }

            message = null!;
            return false;
        }
    }
}
