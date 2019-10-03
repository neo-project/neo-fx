using NeoFx.Storage.RocksDb;
using System;
using System.IO;

namespace StorageExperimentation
{
    internal static class Program
    {
        private static void Main()
        {
            var cpArchivePath = Path.GetFullPath("./cp1.neo-express-checkpoint");
            if (!File.Exists(cpArchivePath))
            {
                throw new Exception("Can't find checkpoint archive");
            }
            Console.WriteLine($"{cpArchivePath}");

            var cpTempPath = Path.Combine(Path.GetTempPath(), $"NeoFX.StorageExperimentation.{Path.GetRandomFileName()}");
            if (Directory.Exists(cpTempPath))
            {
                Directory.Delete(cpTempPath, true);
            }

            System.IO.Compression.ZipFile.ExtractToDirectory(cpArchivePath, cpTempPath);
            Console.WriteLine(cpTempPath);

            using var storage = new RocksDbStore(cpTempPath);
            if (storage.TryGetBlock(0, out var block))
            {
                for (int i = 0; i < block.Transactions.Length; i++)
                {
                    var tx = block.Transactions.Span[i];
                    Console.WriteLine(tx.Type);
                }
            }
        }
    }
}
