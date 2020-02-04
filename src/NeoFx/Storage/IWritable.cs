using DevHawk.Buffers;

namespace NeoFx.Storage
{
    public interface IWritable<T>
    {
        int Size { get; }
        void WriteTo(ref BufferWriter<byte> writer);
    }
}
