using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace dotnetCampus.FileDownloader
{
    /// <summary>
    /// 提供双缓存 线程安全列表
    /// </summary>
    /// 写入的时候写入到一个列表，通过 SwitchBuffer 方法，可以切换当前缓存
    class DoubleBuffer<T>
    {
        public DoubleBuffer()
        {
            CurrentList = AList;
        }

        public void Add(T t)
        {
            lock (_lock)
            {
                CurrentList.Add(t);
            }
        }

        public List<T> SwitchBuffer()
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
        public void DoAll(Action<List<T>> action)
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
        public async Task DoAllAsync(Func<List<T>, Task> action)
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

        private List<T> CurrentList { set; get; }

        private List<T> AList { get; } = new List<T>();
        private List<T> BList { get; } = new List<T>();
    }
}