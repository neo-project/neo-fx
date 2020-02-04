using System;
using System.Diagnostics;
using DevHawk.Buffers;
using NeoFx.Storage;

namespace NeoFx.P2P.Messages
{
    public readonly struct PingPongPayload : IWritable<PingPongPayload>
    {
        public readonly uint LastBlockIndex;
        public readonly DateTimeOffset Timestamp;
        public readonly uint Nonce;

        public PingPongPayload(uint lastBlockIndex, uint nonce, DateTimeOffset timestamp = default)
        {
            if (timestamp == default)
            {
                timestamp = DateTimeOffset.UtcNow;
            }

            LastBlockIndex = lastBlockIndex;
            Timestamp = timestamp;
            Nonce = nonce;
        }

        public const int Size = sizeof(uint) * 3;

        int IWritable<PingPongPayload>.Size => Size;

        public static bool TryRead(ref BufferReader<byte> reader, out PingPongPayload payload)
        {
            if (reader.TryReadLittleEndian(out uint lastBlockIndex)
                && reader.TryReadLittleEndian(out uint timestamp)
                && reader.TryReadLittleEndian(out uint nonce))
            {
                payload = new PingPongPayload(
                    lastBlockIndex,
                    nonce,
                    DateTimeOffset.FromUnixTimeSeconds(timestamp));
                return true;
            }
            payload = default;
            return false;
        }

        public void WriteTo(ref BufferWriter<byte> writer)
        {
            var timestamp = Timestamp.ToUnixTimeSeconds();
            Debug.Assert(timestamp <= uint.MaxValue);

            writer.WriteLittleEndian(LastBlockIndex);
            writer.WriteLittleEndian((uint)timestamp);
            writer.WriteLittleEndian(Nonce);
        }
    }
}
