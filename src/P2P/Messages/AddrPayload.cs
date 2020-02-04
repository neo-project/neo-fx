using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using DevHawk.Buffers;
using NeoFx.Storage;

namespace NeoFx.P2P.Messages
{
    public readonly struct AddrPayload : IWritable<AddrPayload>
    {
        public readonly ImmutableArray<NetworkAddressWithTime> Addresses;

        public AddrPayload(ImmutableArray<NetworkAddressWithTime> addresses)
        {
            Addresses = addresses;
        }

        public int Size => Addresses.GetVarSize(NetworkAddressWithTime.Size);

        public static bool TryRead(ref BufferReader<byte> reader, out AddrPayload payload)
        {
            if (reader.TryReadVarArray<NetworkAddressWithTime, NetworkAddressWithTime.Factory>(out var addresses))
            {
                payload = new AddrPayload(addresses);
                return true;
            }

            payload = default!;
            return false;
        }

        public void WriteTo(ref BufferWriter<byte> writer)
        {
            writer.WriteVarArray(Addresses);
        }
    }
}
