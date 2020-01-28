using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using DevHawk.Buffers;
using Microsoft.Extensions.Logging;
using NeoFx.Storage;

namespace NeoFx.P2P.Messages
{
    public sealed class AddrMessage : Message
    {
        public const string CommandText = "addr";

        public readonly ImmutableArray<NetworkAddressWithTime> Addresses;

        public AddrMessage(in MessageHeader header, in ImmutableArray<NetworkAddressWithTime> addresses) : base(header)
        {
            Addresses = addresses;
        }

        public override void LogMessage(ILogger logger)
        {
            logger.LogInformation("Receive {messageType} {addressCount}",
                nameof(AddrMessage),
                Addresses.Length);
        }

        public static bool TryRead(ref BufferReader<byte> reader, in MessageHeader header, [MaybeNullWhen(false)] out AddrMessage message)
        {
            if (reader.TryReadVarArray<NetworkAddressWithTime, NetworkAddressWithTime.Factory>(out var addresses))
            {
                message = new AddrMessage(header, addresses);
                return true;
            }

            message = default!;
            return false;
        }

    }
}
