using System.Buffers;

namespace dotnetCampus.FileDownloader
{
    // 用于后续支持 .NET 4.5 版本，此版本没有 ArrayPool 类
    public static class SharedArrayPool
    {
        public static byte[] Rent(int minLength)
        {
            return ArrayPool<byte>.Shared.Rent(minLength);
        }

        public static void Return(byte[] array)
        {
            ArrayPool<byte>.Shared.Return(array);
        }
    }
}