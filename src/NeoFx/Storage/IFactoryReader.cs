using DevHawk.Buffers;

namespace NeoFx.Storage
{
    public interface IFactoryReader<T>
    {
        bool TryReadItem(ref BufferReader<byte> reader, out T value);
    }
}
