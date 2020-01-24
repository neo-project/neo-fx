using DevHawk.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace NeoFx.Storage
{
    public interface IFactoryReader<T>
    {
        bool TryReadItem(ref BufferReader<byte> reader, [MaybeNullWhen(false)] out T value);
    }
}
