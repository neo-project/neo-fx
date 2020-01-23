using DevHawk.Buffers;

namespace NeoFx.Storage
{
    public interface IWritable<T>
    {
        void WriteTo(ref BufferWriter<byte> writer);
    }
}
