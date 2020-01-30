using NeoFx.Storage;

namespace NeoFx.P2P
{
    public interface IPayload<T> : IWritable<T>
    {
        int Size {get;}
    }
}
