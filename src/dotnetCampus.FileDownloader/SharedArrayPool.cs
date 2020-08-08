using System.Buffers;

namespace dotnetCampus.FileDownloader
{
    // 用于后续支持 .NET 4.5 版本，此版本没有 ArrayPool 类
    public interface ISharedArrayPool
    {
        byte[] Rent(int minLength);
        void Return(byte[] array);
    }

    public class SharedArrayPool : ISharedArrayPool
    {
        public SharedArrayPool(ArrayPool<byte>? arrayPool = null)
        {
            ArrayPool = arrayPool ?? ArrayPool<byte>.Shared;
        }

        public ArrayPool<byte> ArrayPool
        {
            get;
        }

        public byte[] Rent(int minLength)
        {
            return ArrayPool.Rent(minLength);
        }

        public void Return(byte[] array)
        {
            ArrayPool.Return(array);
        }
    }
}