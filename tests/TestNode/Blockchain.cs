using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoFx.Models;

namespace NeoFx.TestNode
{
    interface IBlockchain : IDisposable
    {
        Task<(uint index, UInt256 hash)> GetLastBlockHash();
        Task<(bool success, UInt256 start, UInt256 stop)> TryGetBlockGap();
        Task AddBlock(in Block block);
    }

    class Blockchain : IBlockchain
    {
        readonly IStorage storage;
        readonly ILogger<Blockchain> log;

        public Blockchain(IStorage storage,
            ILogger<Blockchain> logger)
        {
            this.storage = storage;
            this.log = logger;
        }

        public void Dispose()
        {
            storage.Dispose();
        }

        public Task<(uint index, UInt256 hash)> GetLastBlockHash()
        {
            var tuple = storage.GetLastBlockHash();
            return Task.FromResult(tuple); 
        }

        public Task<(bool success, UInt256 start, UInt256 stop)> TryGetBlockGap()
        {
            var result = storage.TryGetBlockGap(out var start, out var stop);
            return Task.FromResult((result, start, stop));
        }

        public Task AddBlock(in Block block)
        {
            storage.AddBlock(block);
            return Task.CompletedTask;
        }
    }
}
