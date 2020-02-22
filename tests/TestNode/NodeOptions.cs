using System;
using System.Buffers.Binary;
using System.IO;

namespace NeoFx.TestNode
{
    public class NodeOptions
    {
        public string UserAgent { get; set; } = string.Empty;
        public uint Nonce { get; set; } = GetRandomNonce();
        public string StoragePath { get; set; } = GetDefaultStoragePath();

        static string GetDefaultStoragePath() =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".neofx-test-node");
        
        static uint GetRandomNonce() 
        {
            Span<byte> span = stackalloc byte[4];
            StaticRandom.NextBytes(span);
            return BinaryPrimitives.ReadUInt32LittleEndian(span);
        }
    }
}
