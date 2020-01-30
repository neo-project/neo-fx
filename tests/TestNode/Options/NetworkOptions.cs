using System;
using System.Collections.Generic;

namespace NeoFx.TestNode.Options
{
    public class NetworkOptions
    {
        public uint Magic { get; set; }
        public string[] Seeds { get; set; } = Array.Empty<string>();
        public string[] Validators { get; set; } = Array.Empty<string>();

        private (string address, int port) ParseSeed(string seed)
        {
            var colonIndex = seed.IndexOf(':');
            var address = seed.Substring(0, colonIndex);
            var port = int.Parse(seed.AsSpan().Slice(colonIndex + 1));
            return (address, port);
        }

        public IEnumerable<(string address, int port)> GetSeeds()
        {
            for (int i = 0; i < Seeds.Length; i++)
            {
                yield return ParseSeed(Seeds[i]);
            }
        }

        public (string address, int port) GetRandomSeed()
        {
            var random = new Random();
            return ParseSeed(Seeds[random.Next(0, Seeds.Length)]);
        }

        // public IEnumerable<ECPoint> GetValidators()
        // {
        //     var curve = ECCurve.NamedCurves.nistP256.GetExplicit();

        //     for (int i = 0; i < Validators.Length; i++)
        //     {
        //         var bytes = new byte[Validators[i].Length / 2];
        //         if (Validators[i].TryConvertHexString(bytes, out var written)
        //             && (new EncodedPublicKey(bytes)).TryDecode(curve, out var point))
        //         {
        //             yield return point;
        //         }
        //     }
        // }
    }
}
