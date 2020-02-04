using System;
using System.Buffers;
using System.Diagnostics;
using System.Net;
using DevHawk.Buffers;
using NeoFx.Storage;

namespace NeoFx.P2P.Messages
{
    public readonly struct NetworkAddressWithTime : IWritable<NetworkAddressWithTime>
    {
        public readonly struct Factory : IFactoryReader<NetworkAddressWithTime>
        {
            public bool TryReadItem(ref BufferReader<byte> reader, out NetworkAddressWithTime value) => TryRead(ref reader, out value);
        }

        public readonly DateTimeOffset Timestamp;
        public readonly ulong Services;
        public readonly IPEndPoint EndPoint;

        const int ADDRESS_SIZE = 16;
        public const ulong NODE_NETWORK = 1;
        public const int Size = 30;

        int IWritable<NetworkAddressWithTime>.Size => Size;

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
                Span<byte> addressBuffer = stackalloc byte[ADDRESS_SIZE];
                if (reader.TryCopyTo(addressBuffer))
                {
                    reader.Advance(ADDRESS_SIZE);
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

        public void WriteTo(ref BufferWriter<byte> writer)
        {
            var timestamp = Timestamp.ToUnixTimeSeconds();
            Debug.Assert(timestamp <= uint.MaxValue);
            var port = EndPoint.Port;
            Debug.Assert(port <= ushort.MaxValue);

            writer.WriteLittleEndian((uint)timestamp);
            writer.WriteLittleEndian(Services);
            {
                using var owner = MemoryPool<byte>.Shared.Rent(ADDRESS_SIZE);
                var span = owner.Memory.Span.Slice(0, ADDRESS_SIZE);
                if (!EndPoint.Address.TryWriteBytes(span, out var bytesWritten)
                    || bytesWritten != ADDRESS_SIZE)
                {
                    throw new InvalidOperationException();
                }
            }
            writer.WriteLittleEndian((ushort)port);
        }
    }
}
