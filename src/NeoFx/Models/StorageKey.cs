using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace NeoFx.Models
{
    public readonly struct StorageKey
    {
        public readonly UInt160 ScriptHash;
        public readonly ImmutableArray<byte> Key;

        public StorageKey(UInt160 scriptHash, ImmutableArray<byte> key)
        {
            ScriptHash = scriptHash;
            Key = key;
        }
    }
}
