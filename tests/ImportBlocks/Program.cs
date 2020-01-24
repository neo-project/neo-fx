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
    class Program
    {
        static void Main(string[] args) => CommandLineApplication.Execute<Program>(args);

        [Argument(0)]
        public string OfflinePackage { get; } = string.Empty;

        [Argument(1)]
        public string DatabaseDirectory { get; } = string.Empty;

        public void OnExecute()
        {
            var offlinePackage = OfflinePackage.Length > 0
                ? OfflinePackage
                : @"C:\Users\harry\Source\neo\seattle\chain.acc.zip";

            var dbDirectory = DatabaseDirectory.Length > 0
                ? DatabaseDirectory
                : @" C:\Users\harry\Source\neo\seattle\fx\ImportTest";

            using var archiveFileStream = new FileStream(offlinePackage, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new ZipArchive(archiveFileStream, ZipArchiveMode.Read);
            using var archiveStream = archive.GetEntry("chain.acc").Open();
            using var archiveReader = new BinaryReader(archiveStream);

            var count = archiveReader.ReadUInt32();
            var pool = ArrayPool<byte>.Shared;
            for (var index = 0; index < count; index++)
            {
                var size = (int)archiveReader.ReadUInt32();
                var array = pool.Rent(size);
                var bytesRead = archiveReader.Read(array.AsSpan(0, size));
                Debug.Assert(bytesRead == size);

                var reader = new BufferReader<byte>(array.AsSpan(0, size));
                var succeeded = NeoFx.Models.Block.TryRead(ref reader, out var block);
                Debug.Assert(succeeded);
                Debug.Assert(reader.End);

                var blockHash = block.CalculateHash();

                if (index % 1000 == 0) Console.WriteLine($"{index}\t\t{blockHash}");

                pool.Return(array);
            }
        }
    }
}
