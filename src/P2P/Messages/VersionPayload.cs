using System;
using DevHawk.Buffers;
using NeoFx.Storage;

namespace NeoFx.P2P.Messages
{
    public readonly struct VersionPayload
    {
        public const uint PROTOCOL_VERSION = 0;
        public const ulong NODE_NETWORK = 1;

        public readonly uint Version;
        public readonly ulong Services;
        public readonly DateTimeOffset Timestamp;
        public readonly ushort Port;
        public readonly uint Nonce;
        public readonly string UserAgent;
        public readonly uint StartHeight;
        public readonly bool Relay;

        public int Size
        {
            get
            {
                const int size = (sizeof(uint) * 4) + sizeof(ulong) + sizeof(ushort) + sizeof(byte);
                return size + UserAgent.GetVarSize();
            }
        }
        
        public VersionPayload(uint nonce, string userAgent, ushort port = 0,
            uint startHeight = 0, bool relay = true, uint version = PROTOCOL_VERSION, ulong services = NODE_NETWORK,
            DateTimeOffset timestamp = default)
        {
            Version = version;
            Services = services;
            Timestamp = timestamp == default ? DateTimeOffset.UtcNow : timestamp;
            Port = port;
            Nonce = nonce;
            UserAgent = userAgent;
            StartHeight = startHeight;
            Relay = relay;
        }

        public static bool TryRead(ref BufferReader<byte> reader, out VersionPayload payload)
        {
            if (reader.TryReadLittleEndian(out uint version)
                && reader.TryReadLittleEndian(out ulong services)
                && reader.TryReadLittleEndian(out uint timestamp)
                && reader.TryReadLittleEndian(out ushort port)
                && reader.TryReadLittleEndian(out uint nonce)
                && reader.TryReadVarString(out var userAgent)
                && reader.TryReadLittleEndian(out uint startHeight)
                && reader.TryRead(out byte relay))
            {
                payload = new VersionPayload(
                    nonce: nonce,
                    userAgent: userAgent,
                    port: port,
                    startHeight: startHeight,
                    relay: relay != 0,
                    version: version,
                    services: services,
                    timestamp: DateTimeOffset.FromUnixTimeSeconds(timestamp));
                return true;
            }

            payload = default;
            return false;

        }
    }
}
