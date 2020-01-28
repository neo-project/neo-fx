using System;
using DevHawk.Buffers;

namespace NeoFx.P2P.Messages
{
    public readonly struct PingPongPayload
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
    }
}
