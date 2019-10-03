using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace NeoFx.Models
{
    public readonly struct TransactionAttribute
    {
        public enum UsageType : byte
        {
            ContractHash = 0x00,

            ECDH02 = 0x02,
            ECDH03 = 0x03,

            Script = 0x20,

            Vote = 0x30,

            DescriptionUrl = 0x81,
            Description = 0x90,

            Hash1 = 0xa1,
            Hash2 = 0xa2,
            Hash3 = 0xa3,
            Hash4 = 0xa4,
            Hash5 = 0xa5,
            Hash6 = 0xa6,
            Hash7 = 0xa7,
            Hash8 = 0xa8,
            Hash9 = 0xa9,
            Hash10 = 0xaa,
            Hash11 = 0xab,
            Hash12 = 0xac,
            Hash13 = 0xad,
            Hash14 = 0xae,
            Hash15 = 0xaf,

            Remark = 0xf0,
            Remark1 = 0xf1,
            Remark2 = 0xf2,
            Remark3 = 0xf3,
            Remark4 = 0xf4,
            Remark5 = 0xf5,
            Remark6 = 0xf6,
            Remark7 = 0xf7,
            Remark8 = 0xf8,
            Remark9 = 0xf9,
            Remark10 = 0xfa,
            Remark11 = 0xfb,
            Remark12 = 0xfc,
            Remark13 = 0xfd,
            Remark14 = 0xfe,
            Remark15 = 0xff
        }

        public readonly UsageType Usage;
        public readonly ReadOnlyMemory<byte> Data;

        public readonly int Size => Data.Length + 1;

        public TransactionAttribute(UsageType usage, ReadOnlyMemory<byte> data)
        {
            Usage = usage;
            Data = data;
        }

        public static bool TryRead(ref SequenceReader<byte> reader, out TransactionAttribute value)
        {
            if (reader.TryRead(out byte usage)
                && TryReadData(ref reader, (UsageType)usage, out var data))
            {
                value = new TransactionAttribute((UsageType)usage, data);
                return true;
            }

            value = default;
            return false;
        }

        private static bool TryReadData(ref SequenceReader<byte> reader, UsageType usage, out ReadOnlyMemory<byte> value)
        {
            static bool TryCopyToAndAdvance(ref SequenceReader<byte> reader, Span<byte> buffer)
            {
                if (reader.TryCopyTo(buffer))
                {
                    reader.Advance(buffer.Length);
                    return true;
                }

                return false;
            }

            switch (usage)
            {
                case UsageType.ContractHash:
                case UsageType.Vote:
                case var _ when usage >= UsageType.Hash1 && usage <= UsageType.Hash15:
                    return reader.TryReadByteArray(32, out value);
                case UsageType.Script:
                    return reader.TryReadByteArray(20, out value);
                case UsageType.Description:
                case var _ when usage >= UsageType.Remark:
                    return reader.TryReadVarArray(out value);
                case UsageType.ECDH02:
                case UsageType.ECDH03:
                    {
                        var buffer = new byte[33];
                        buffer[0] = (byte)usage;

                        if (reader.TryCopyTo(buffer.AsSpan().Slice(1)))
                        {
                            reader.Advance(32);
                            value = buffer;
                            return true;
                        }
                    }
                    break;
                case UsageType.DescriptionUrl:
                    if (reader.TryRead(out var size))
                    {
                        return reader.TryReadByteArray(size, out value);
                    }
                    break;
            }

            value = default;
            return false;
        }
    }
}
