using DevHawk.Buffers;

namespace NeoFx.Storage
{
    public static class WritableHelpers
    {
        public static void Write<T>(ref this BufferWriter<byte> writer, in T value)
            where T : IWritable<T>
        {
            value.Write(ref writer);
        }
    }
}
