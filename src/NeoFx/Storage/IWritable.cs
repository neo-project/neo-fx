using DevHawk.Buffers;

namespace NeoFx.Storage
{
    public interface IWritable<T>
    {
        void Write(ref BufferWriter<byte> writer);
    }
}
