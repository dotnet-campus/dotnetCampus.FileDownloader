using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace dotnetCampus.FileDownloader
{
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