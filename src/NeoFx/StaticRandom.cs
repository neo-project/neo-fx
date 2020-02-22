using System;
using System.Numerics;

namespace NeoFx
{
    // https://devblogs.microsoft.com/pfxteam/getting-random-numbers-in-a-thread-safe-way/
    public static class StaticRandom
    {
        private static Random _global = new Random();
    
        [ThreadStatic]
        private static Random? _local;

        private static Random ThreadStaticRandom 
        {
            get
            {
                Random? inst = _local;
                if (inst == null)
                {
                    int seed;
                    lock (_global) seed = _global.Next();
                    _local = inst = new Random(seed);
                }
                return inst;
            }
        }

        public static int Next() => ThreadStaticRandom.Next();

        public static int Next(int maxValue) => ThreadStaticRandom.Next(maxValue);

        public static int Next(int minValue, int maxValue) => ThreadStaticRandom.Next(minValue, maxValue);

        public static void NextBytes(Span<byte> buffer) => ThreadStaticRandom.NextBytes(buffer);

        public static double NextDouble() => ThreadStaticRandom.NextDouble();
    }
}
