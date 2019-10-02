using System;
using System.IO;

namespace StorageExperimentation
{
    class Program
    {
        static void Main(string[] args)
        {
            var foo = File.Exists("./cp1/000033.sst");
            Console.WriteLine($"{foo} {Environment.CurrentDirectory}");
        }
    }
}
