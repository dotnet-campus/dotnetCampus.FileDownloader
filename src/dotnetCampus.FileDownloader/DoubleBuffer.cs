using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace dotnetCampus.FileDownloader
{
    /// <summary>
    /// 提供双缓存 线程安全列表
    /// </summary>
    /// 写入的时候写入到一个列表，通过 SwitchBuffer 方法，可以切换当前缓存
    class DoubleBuffer<T> : DoubleBuffer<List<T>, T>
    {
        public DoubleBuffer() : base(new List<T>(), new List<T>())
        {
        }
    }

    /// <summary>
    /// 提供双缓存 线程安全列表
    /// </summary>
    /// 写入的时候写入到一个列表，通过 SwitchBuffer 方法，可以切换当前缓存
    class DoubleBuffer<T, TU> where T : class, ICollection<TU>
    {
        public DoubleBuffer(T aList, T bList)
        {
            AList = aList;
            BList = bList;

            CurrentList = AList;
        }

        public void Add(TU t)
        {
            lock (_lock)
            {
                CurrentList.Add(t);
            }
        }

        public T SwitchBuffer()
        {
            lock (_lock)
            {
                if (ReferenceEquals(CurrentList, AList))
                {
                    CurrentList = BList;
                    return AList;
                }
                else
                {
                    CurrentList = AList;
                    return BList;
                }
            }
        }

        /// <summary>
        /// 执行完所有任务
        /// </summary>
        /// <param name="action">当前缓存里面存在的任务，请不要保存传入的 List 参数</param>
        public void DoAll(Action<T> action)
        {
            while (true)
            {
                var buffer = SwitchBuffer();
                if (buffer.Count == 0) break;

                action(buffer);
                buffer.Clear();
            }
        }

        /// <summary>
        /// 执行完所有任务
        /// </summary>
        /// <param name="action">当前缓存里面存在的任务，请不要保存传入的 List 参数</param>
        /// <returns></returns>
        public async Task DoAllAsync(Func<T, Task> action)
        {
            while (true)
            {
                var buffer = SwitchBuffer();
                if (buffer.Count == 0) break;

                await action(buffer);
                buffer.Clear();
            }
        }

        private readonly object _lock = new object();

        private T CurrentList { set; get; }

        private T AList { get; }
        private T BList { get; }
    }
}