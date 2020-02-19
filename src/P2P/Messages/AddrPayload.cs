using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using DevHawk.Buffers;
using NeoFx.Storage;

namespace NeoFx.P2P.Messages
{
    public readonly struct AddrPayload : IWritable<AddrPayload>
    {
        public readonly ImmutableArray<NodeAddress> Addresses;

        public AddrPayload(ImmutableArray<NodeAddress> addresses)
        {
            Addresses = addresses;
        }

        public int Size => Addresses.GetVarSize(NodeAddress.Size);

        public static bool TryRead(ref BufferReader<byte> reader, out AddrPayload payload)
        {
            if (reader.TryReadVarArray<NodeAddress>(NodeAddress.TryRead, out var addresses))
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
