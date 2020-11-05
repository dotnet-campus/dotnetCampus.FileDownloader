namespace dotnetCampus.FileDownloader
{
#if NETCOREAPP
    using System.Buffers;

    /// <summary>
    /// 共享数组内存，底层使用 ArrayPool 实现
    /// </summary>
    public class SharedArrayPool : ISharedArrayPool
    {
        /// <summary>
        /// 创建共享数组
        /// </summary>
        /// <param name="arrayPool"></param>
        public SharedArrayPool(ArrayPool<byte>? arrayPool = null)
        {
            ArrayPool = arrayPool ?? ArrayPool<byte>.Shared;
        }

        /// <summary>
        /// 使用的数组池
        /// </summary>
        public ArrayPool<byte> ArrayPool
        {
            get;
        }

        /// <inheritdoc />
        public byte[] Rent(int minLength)
        {
            return ArrayPool.Rent(minLength);
        }

        /// <inheritdoc />
        public void Return(byte[] array)
        {
            ArrayPool.Return(array);
        }
    }
#else
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// 共享数组内存，底层使用 List&lt;WeakReference&lt;byte[]&gt;&gt; 实现
    /// </summary>
    class SharedArrayPool : ISharedArrayPool
    {
        public const int BufferLength = ushort.MaxValue;

        public byte[] Rent(int minLength)
        {
            if (minLength != BufferLength)
            {
                throw new ArgumentException($"Can not receive minLength!={BufferLength}");
            }

            lock (Pool)
            {
                for (var i = 0; i < Pool.Count; i++)
                {
                    var reference = Pool[i];
                    if (reference.TryGetTarget(out var byteList))
                    {
                        Pool.RemoveAt(i);
                        return byteList;
                    }
                    else
                    {
                        Pool.RemoveAt(i);
                        i--;
                    }
                }
            }

            return new byte[BufferLength];
        }

        public void Return(byte[] array)
        {
            lock (Pool)
            {
                Pool.Add(new WeakReference<byte[]>(array));
            }
        }

        /// <summary>
        /// 客户端程序在下载完成之后强行回收内存
        /// </summary>
        public void Clean()
        {
            lock (Pool)
            {
                GC.Collect();
                GC.WaitForFullGCComplete();

                Pool.RemoveAll(reference => !reference.TryGetTarget(out _));

                Pool.Capacity = Pool.Count;
            }
        }

        private List<WeakReference<byte[]>> Pool { get; } = new List<WeakReference<byte[]>>();
    }
#endif
}
