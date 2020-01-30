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
                    case GetAddrMessage.CommandText:
                        message = new GetAddrMessage(header);
                        return true;
                    case VerAckMessage.CommandText:
                        message = new VerAckMessage(header);
                        return true;
                }
            }
            else
            {
                var reader = new BufferReader<byte>(sequence);
                reader.Advance(MessageHeader.Size);
                switch (header.Command)
                {
                    case AddrMessage.CommandText:
                        {
                            if (AddrMessage.TryRead(ref reader, header, out var _message))
                            {
                                message = _message;
                                return true;
                            }
                        }
                        break;
                    case BlockMessage.CommandText:
                        {
                            if (BlockMessage.TryRead(ref reader, header, out var _message))
                            {
                                message = _message;
                                return true;
                            }
                        }
                        break;
                    case ConsensusMessage.CommandText:
                        {
                            if (ConsensusMessage.TryRead(ref reader, header, out var _message))
                            {
                                message = _message;
                                return true;
                            }
                        }
                        break;
                    case GetBlocksMessage.CommandText:
                        {
                            if (GetBlocksMessage.TryRead(ref reader, header, out var _message))
                            {
                                message = _message;
                                return true;
                            }
                        }
                        break;
                    case GetDataMessage.CommandText:
                        {
                            if (GetDataMessage.TryRead(ref reader, header, out var _message))
                            {
                                message = _message;
                                return true;
                            }
                        }
                        break;
                    case GetHeadersMessage.CommandText:
                        {
                            if (GetHeadersMessage.TryRead(ref reader, header, out var _message))
                            {
                                message = _message;
                                return true;
                            }
                        }
                        break;
                    case HeadersMessage.CommandText:
                        {
                            if (HeadersMessage.TryRead(ref reader, header, out var _message))
                            {
                                message = _message;
                                return true;
                            }
                        }
                        break;
                    case InvMessage.CommandText:
                        {
                            if (InvMessage.TryRead(ref reader, header, out var _message))
                            {
                                message = _message;
                                return true;
                            }
                        }
                        break;
                    case PingMessage.CommandText:
                        {
                            if (PingMessage.TryRead(ref reader, header, out var _message))
                            {
                                message = _message;
                                return true;
                            }
                        }
                        break;
                    case PongMessage.CommandText:
                        {
                            if (PongMessage.TryRead(ref reader, header, out var _message))
                            {
                                message = _message;
                                return true;
                            }
                        }
                        break;
                    case TransactionMessage.CommandText:
                        {
                            if (TransactionMessage.TryRead(ref reader, header, out var _message))
                            {
                                message = _message;
                                return true;
                            }
                        }
                        break;
                    case VersionMessage.CommandText:
                        {
                            if (VersionMessage.TryRead(ref reader, header, out var _message))
                            {
                                message = _message;
                                return true;
                            }
                        }
                        break;

                        // filteradd, filterclear, filterload
                        // mempool
                }
            }

            message = null!;
            return false;
        }
    }
}
