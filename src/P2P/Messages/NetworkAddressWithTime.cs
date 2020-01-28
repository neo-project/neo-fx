using System;
using System.Net;
using DevHawk.Buffers;
using NeoFx.Storage;

namespace NeoFx.P2P.Messages
{
    public readonly struct NetworkAddressWithTime
    {
        public readonly struct Factory : IFactoryReader<NetworkAddressWithTime>
        {
            public bool TryReadItem(ref BufferReader<byte> reader, out NetworkAddressWithTime value) => TryRead(ref reader, out value);
        }

        public readonly DateTimeOffset Timestamp;
        public readonly ulong Services;
        public readonly IPEndPoint EndPoint;

        public const ulong NODE_NETWORK = 1;
        public const int BufferSize = 30;

        public NetworkAddressWithTime(
            IPEndPoint endpoint,
            DateTimeOffset timestamp = default,
            ulong services = NODE_NETWORK)
        {
            Timestamp = timestamp == default ? DateTimeOffset.UtcNow : timestamp;
            Services = services;
            EndPoint = endpoint;
        }

        public static bool TryRead(ref BufferReader<byte> reader, out NetworkAddressWithTime value)
        {
            static bool TryReadAddress(ref BufferReader<byte> reader, out IPAddress address)
            {
                Span<byte> addressBuffer = stackalloc byte[16];
                if (reader.TryCopyTo(addressBuffer))
                {
                    reader.Advance(16);
                    address = new IPAddress(addressBuffer);
                    return true;
                }

                address = null!;
                return false;
            }

            Span<byte> addressBuffer = stackalloc byte[4];
            if (reader.TryReadLittleEndian(out uint timestamp)
                && reader.TryReadLittleEndian(out ulong services)
                && TryReadAddress(ref reader, out var address)
                && reader.TryReadBigEndian(out ushort port))
            {
                value = new NetworkAddressWithTime(
                    new IPEndPoint(address, port),
                    DateTimeOffset.FromUnixTimeSeconds(timestamp),
                    services);
                return true;
            }

            value = default;
            return false;
        }
    }
}
