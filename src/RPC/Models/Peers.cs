using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;

namespace NeoFx.RPC.Models
{
    public readonly struct Peers
    {
        public readonly ImmutableArray<(IPAddress address, int port)> Unconnected;
        public readonly ImmutableArray<(IPAddress address, int port)> Connected;

        public Peers(IEnumerable<(IPAddress address, int port)> unconnected, IEnumerable<(IPAddress address, int port)> connected)
        {
            Unconnected = unconnected.ToImmutableArray();
            Connected = connected.ToImmutableArray();
        }
    }
}
