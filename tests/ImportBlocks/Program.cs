using DevHawk.Buffers;
using McMaster.Extensions.CommandLineUtils;
using NeoFx;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace ImportBlocks
{
    using NeoBlock = Neo.Network.P2P.Payloads.Block;

    class Program
    {
        static void Main(string[] args) => CommandLineApplication.Execute<Program>(args);

        [Argument(0)]
        public string OfflinePackage { get; } = string.Empty;

        [Argument(1)]
        public string DatabaseDirectory { get; } = string.Empty;

        static NeoBlock DeserializeNeoBlock(byte[] array)
        {
            return Neo.IO.Helper.AsSerializable<NeoBlock>(array);
        }

        static bool CompareUInt256(in NeoFx.UInt256 fx, Neo.UInt256 neo)
        {
            Span<byte> fxBuffer = stackalloc byte[32];
            fx.Write(fxBuffer);
            return fxBuffer.SequenceEqual(neo.ToArray());
        }

        static bool CompareBlock(in NeoFx.Models.Block fxBlock, byte[] array)
        {
            var neoBlock = DeserializeNeoBlock(array);
            if (!CompareUInt256(fxBlock.CalculateHash(), neoBlock.Hash)) return false;
            if (fxBlock.Transactions.Length != neoBlock.Transactions.Length) return false;

            for (var i = 0; i < fxBlock.Transactions.Length; i++)
            {
                var fxTx = fxBlock.Transactions[i];
                var neoTx = neoBlock.Transactions[i];

                if (!CompareUInt256(fxTx.CalculateHash(), neoTx.Hash)) return false;
            }

            return true;
        }

        public void OnExecute()
        {
            var offlinePackage = OfflinePackage.Length > 0
                ? OfflinePackage
                : @"C:\Users\harry\Source\neo\seattle\chain.acc.zip";

            var dbDirectory = DatabaseDirectory.Length > 0
                ? DatabaseDirectory
                : @" C:\Users\harry\Source\neo\seattle\fx\ImportTest";

            var sw = System.Diagnostics.Stopwatch.StartNew();

            using var archiveFileStream = new FileStream(offlinePackage, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new ZipArchive(archiveFileStream, ZipArchiveMode.Read);
            using var archiveStream = archive.GetEntry("chain.acc").Open();
            using var archiveReader = new BinaryReader(archiveStream);

            var count = archiveReader.ReadUInt32();
            // Console.WriteLine(count);

            var pool = ArrayPool<byte>.Shared;
            for (var index = 0; index < count; index++)
            {
                var size = (int)archiveReader.ReadUInt32();
                var array = pool.Rent(size);
                var bytesRead = archiveReader.Read(array.AsSpan(0, size));
                Debug.Assert(bytesRead == size);
                // var reader = new BufferReader<byte>(array.AsSpan(0, size));
                // var succeeded = NeoFx.Models.Block.TryRead(ref reader, out var block);
                // Debug.Assert(succeeded);
                // Debug.Assert(reader.End);
                // Debug.Assert(CompareBlock(block, array));
                var neoBlock = DeserializeNeoBlock(array);

                // if (index % 1000 == 0) Console.WriteLine($"{index}");

                pool.Return(array);
            }

            sw.Stop();
            Console.WriteLine($"ImportBlocks took {sw.ElapsedMilliseconds}ms");
        }
    }
}
