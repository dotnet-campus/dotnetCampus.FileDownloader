using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace dotnetCampus.FileDownloader
{
    class DoubleBufferTask<T>
    {
        public DoubleBufferTask(Func<List<T>, Task> doTask)
        {
            _doTask = doTask;
        }

        public void AddTask(T t)
        {
            DoubleBuffer.Add(t);

            DoInner();
        }

        private async void DoInner()
        {
            // ReSharper disable once InconsistentlySynchronizedField
            if (_isDoing) return;

            lock (DoubleBuffer)
            {
                if (_isDoing) return;
                _isDoing = true;
            }

            await DoubleBuffer.DoAllAsync(_doTask);

            lock (DoubleBuffer)
            {
                _isDoing = false;
                Finished?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Finish()
        {
            lock (DoubleBuffer)
            {
                if (!_isDoing)
                {
                    FinishTask.SetResult(true);
                    return;
                }

                Finished += (sender, args) => FinishTask.SetResult(true);
            }
        }

        public Task WaitAllTaskFinish()
        {
            return FinishTask.Task;
        }

        private TaskCompletionSource<bool> FinishTask { get; } = new TaskCompletionSource<bool>();

        private bool _isDoing;

        private event EventHandler? Finished;

        private readonly Func<List<T>, Task> _doTask;

        private DoubleBuffer<T> DoubleBuffer { get; } = new DoubleBuffer<T>();
    }
}