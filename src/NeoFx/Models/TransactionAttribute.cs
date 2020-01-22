using DevHawk.Buffers;
using NeoFx.Storage;
using System;
using System.Buffers;
using System.Collections.Immutable;

namespace NeoFx.Models
{
    public readonly struct TransactionAttribute : IFactoryReader<TransactionAttribute>, IWritable<TransactionAttribute>
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
        public readonly ImmutableArray<byte> Data;

        public TransactionAttribute(UsageType usage, ImmutableArray<byte> data)
        {
            Usage = usage;
            Data = data;
        }

        private static bool TryReadAttributeData(ref BufferReader<byte> reader, UsageType usage, out ImmutableArray<byte> value)
        {
            switch (usage)
            {
                case UsageType.ContractHash:
                case UsageType.Vote:
                case UsageType.ECDH02:
                case UsageType.ECDH03:
                case var _ when usage >= UsageType.Hash1 && usage <= UsageType.Hash15:
                    {
                        if (reader.TryReadByteArray(32, out var buffer))
                        {
                            value = buffer;
                            return true;
                        }
                    }
                    break;
                case UsageType.Script:
                    {
                        if (reader.TryReadByteArray(20, out var buffer))
                        {
                            value = buffer;
                            return true;
                        }
                    }
                    break;
                case UsageType.Description:
                case var _ when usage >= UsageType.Remark:
                    return reader.TryReadVarArray(ushort.MaxValue, out value);
                case UsageType.DescriptionUrl:
                    {
                        if (reader.TryRead(out byte length)
                            && reader.TryReadByteArray(length, out var data))
                        {
                            value = data;
                            return true;
                        }
                    }
                    break;
            }

            value = default;
            return false;
        }

        public static bool TryRead(ref BufferReader<byte> reader, out TransactionAttribute value)
        {
            if (reader.TryRead(out byte usage)
                && TryReadAttributeData(ref reader, (UsageType)usage, out var data))
            {
                value = new TransactionAttribute((UsageType)usage, data);
                return true;
            }

            value = default;
            return false;
        }

        bool IFactoryReader<TransactionAttribute>.TryReadItem(ref BufferReader<byte> reader, out TransactionAttribute value) => TryRead(ref reader, out value);

        public void Write(ref BufferWriter<byte> writer)
        {
            writer.WriteLittleEndian((byte)Usage);
            writer.WriteVarArray(Data.AsSpan());
        }
    }
}
