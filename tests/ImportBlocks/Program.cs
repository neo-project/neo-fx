using DevHawk.Buffers;
using McMaster.Extensions.CommandLineUtils;
using NeoFx;
using NeoFx.Models;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace ImportBlocks
{
    using NeoBlock = Neo.Network.P2P.Payloads.Block;
    class Program
    {
        static void Main(string[] args) => CommandLineApplication.Execute<Program>(args);

        [Argument(0)]
        private string OfflinePackage { get; } = string.Empty;

        [Argument(1)]
        private string DatabaseDirectory { get; } = string.Empty;

        [Option]
        private bool Reset { get; }

        static bool ValidateBlock(in Block fxBlock, byte[] array)
        {
            static bool CompareUInt256(in NeoFx.UInt256 fx, Neo.UInt256 neo)
            {
                Span<byte> fxBuffer = stackalloc byte[32];
                fx.Write(fxBuffer);
                return fxBuffer.SequenceEqual(neo.ToArray());
            }

            var neoBlock = Neo.IO.Helper.AsSerializable<NeoBlock>(array);
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
                : System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".neofx-import-blocks");

            if (Reset)
            {
                Directory.Delete(dbDirectory, true);
            }

            using var db = new Database(dbDirectory);

            foreach (var b in db.GetBlocks().OrderBy(t => t.blockState.header.Index))
            {
                Console.WriteLine($"{b.key} {b.blockState.header.Index}");
            }

            using var archiveFileStream = new FileStream(offlinePackage, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new ZipArchive(archiveFileStream, ZipArchiveMode.Read);
            using var archiveStream = archive.GetEntry("chain.acc").Open();
            using var archiveReader = new BinaryReader(archiveStream);

            var count = archiveReader.ReadUInt32();
            var pool = MemoryPool<byte>.Shared;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (var index = 0; index < count; index++)
            {
                var size = (int)archiveReader.ReadUInt32();
                using var owner = pool.Rent(size);
                var span = owner.Memory.Span.Slice(0, size);
                var bytesRead = archiveReader.Read(span);
                Debug.Assert(bytesRead == size);
                var reader = new BufferReader<byte>(span);
                var succeeded = Block.TryRead(ref reader, out var block);
                Debug.Assert(succeeded);
                Debug.Assert(reader.End);

                db.AddBlock(block);

                if (index % 1000 == 0) Console.WriteLine($"{index}");
            }
            sw.Stop();

            Console.WriteLine($"ImportBlocks took {sw.ElapsedMilliseconds}ms");
        }
    }
}
