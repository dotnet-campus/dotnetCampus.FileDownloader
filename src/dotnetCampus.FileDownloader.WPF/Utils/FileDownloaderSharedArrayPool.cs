using System;
using System.Collections.Generic;

namespace dotnetCampus.FileDownloader.WPF.Utils
{
    /// <summary>
    /// 对 WPF 优化的文件下载使用的数组缓存
    /// </summary>
    class FileDownloaderSharedArrayPool : ISharedArrayPool
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
}