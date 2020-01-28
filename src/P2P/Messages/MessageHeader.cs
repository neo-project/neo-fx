using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using DevHawk.Buffers;

namespace NeoFx.P2P.Messages
{
    public readonly struct MessageHeader
    {
        public readonly uint Magic;
        public readonly string Command;
        public readonly uint Length;
        public readonly uint Checksum;

        public const int CommandSize = 12;
        public const int Size = 24;

        public MessageHeader(uint magic, string command, uint length, uint checksum)
        {
            Debug.Assert(command.Length <= CommandSize);

            Magic = magic;
            Command = command;
            Length = length;
            Checksum = checksum;
        }

        public static bool TryRead(ReadOnlySequence<byte> sequence, out MessageHeader value)
        {
            static bool TryReadCommandString(ref BufferReader<byte> reader, out string command) 
            {
                Span<byte> commandBytes = stackalloc byte[CommandSize];
                if (reader.TryCopyTo(commandBytes))
                {
                    reader.Advance(CommandSize);
                    command = Encoding.UTF8.GetString(commandBytes).TrimEnd('\0');
                    return true;
                }

                command = null!;
                return false;
            }

            var reader = new BufferReader<byte>(sequence);
            if (reader.TryReadLittleEndian(out uint magic)
                && TryReadCommandString(ref reader, out var command)
                && reader.TryReadLittleEndian(out uint length)
                && reader.TryReadLittleEndian(out uint checksum))
            {
                value = new MessageHeader(magic, command, length, checksum);
                return true;
            }

            value = default;
            return false;
        }
    }
}
