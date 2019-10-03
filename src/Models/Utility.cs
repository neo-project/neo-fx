using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace NeoFx.Models
{
    internal static class Utility
    {
        public static int GetVarSize(ulong value)
        {
            if (value < 0xfd)
            {
                return sizeof(byte);
            }

            if (value < 0xffff)
            {
                return sizeof(byte) + sizeof(ushort);
            }

            if (value < 0xffffffff)
            {
                return sizeof(byte) + sizeof(uint);
            }

            return sizeof(byte) + sizeof(ulong);
        }

        public static int GetVarSize(this string value)
        {
            int size = System.Text.Encoding.UTF8.GetByteCount(value);
            return GetVarSize((ulong)size) + size;
        }
    }
}
