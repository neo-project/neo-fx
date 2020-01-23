using System.Collections.Immutable;

namespace NeoFx.Models
{
    public readonly struct StorageKey
    {
        public readonly UInt160 ScriptHash;
        public readonly ImmutableArray<byte> Key;

        public const int BlockSize = 16;

        public int Size => UInt160.Size + (((Key.Length / BlockSize) + 1) * (BlockSize + 1));

        public StorageKey(UInt160 scriptHash, ImmutableArray<byte> key)
        {
            ScriptHash = scriptHash;
            Key = key;
        }

        //public static bool TryReadBytes(ReadOnlySpan<byte> span, out StorageKey value)
        //{
        //    // StorageKey.Key uses an atypical storage pattern relative to other models in Neo.
        //    // The byte array is written in blocks of 16 bytes followed by a byte indicating how many
        //    // bytes of the previous block were padding. Only the last block of 16 is allowed to have
        //    // padding. Read blocks of 16 (plus 1 padding indication byte) until padding indication byte
        //    // is greater than zero.

        //    if (UInt160.TryRead(span, out var scriptHash))
        //    {
        //        span = span.Slice(UInt160.Size);
        //        var blockCount = span.Length / (StorageKeyBlockSize + 1);

        //        Debug.Assert((span.Length % (StorageKeyBlockSize + 1)) == 0);
        //        Debug.Assert(blockCount > 0);

        //        var padding = span[span.Length - 1];
        //        var bufferSize = (blockCount * StorageKeyBlockSize) - padding;
        //        var buffer = new byte[bufferSize];

        //        for (int i = 0; i < blockCount; i++)
        //        {
        //            var src = span.Slice(
        //                i * (StorageKeyBlockSize + 1),
        //                StorageKeyBlockSize - ((i == blockCount - 1) ? padding : 0));
        //            var dst = buffer.AsSpan().Slice(i * StorageKeyBlockSize);
        //            src.CopyTo(dst);
        //        }

        //        value = new StorageKey(scriptHash, buffer);
        //        return true;
        //    }

        //    value = default;
        //    return false;
        //}


        //public static bool TryWrite(this StorageKey key, Span<byte> span, out int bytesWritten)
        //{
        //    var keySize = key.GetSize();
        //    if (span.Length >= keySize && key.ScriptHash.TryWrite(span))
        //    {
        //        span = span.Slice(UInt160.Size);
        //        var keySpan = key.Key.Span;

        //        while (keySpan.Length >= StorageKeyBlockSize)
        //        {
        //            keySpan.Slice(0, StorageKeyBlockSize).CopyTo(span);
        //            span[StorageKeyBlockSize] = 0;

        //            keySpan = keySpan.Slice(StorageKeyBlockSize);
        //            span = span.Slice(StorageKeyBlockSize + 1);
        //        }

        //        Debug.Assert(span.Length == StorageKeyBlockSize + 1);

        //        keySpan.CopyTo(span);
        //        span.Slice(keySpan.Length).Clear();
        //        span[StorageKeyBlockSize] = (byte)(StorageKeyBlockSize - keySpan.Length);

        //        bytesWritten = keySize;
        //        return true;
        //    }

        //    bytesWritten = default;
        //    return false;
        //}

    }
}
