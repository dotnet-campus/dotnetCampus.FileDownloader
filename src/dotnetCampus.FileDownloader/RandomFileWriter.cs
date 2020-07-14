using System;
using System.Buffers;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using dotnetCampus.Threading;

namespace dotnetCampus.FileDownloader
{
    /// <summary>
    /// 不按照顺序，随机写入文件
    /// </summary>
    public class RandomFileWriter : IAsyncDisposable
    {
        public RandomFileWriter(FileStream stream)
        {
            Stream = stream;
            Task.Run(WriteToFile);
        }

        /// <summary>
        /// 写入文件，可不等待
        /// </summary>
        /// <param name="fileStartPoint">从文件的哪里开始写</param>
        /// <param name="data">写入的数据</param>
        public async Task WriteAsync(long fileStartPoint, byte[] data)
        {
            var task = new TaskCompletionSource<bool>();

            var fileSegment = new FileSegment(fileStartPoint, data, 0, data.Length, task);
            DownloadSegmentList.Enqueue(fileSegment);
            await task.Task;
        }

        /// <summary>
        /// 写入文件，可不等待
        /// </summary>
        /// <param name="fileStartPoint">从文件的哪里开始写</param>
        /// <param name="data">写入的数据</param>
        /// <param name="dataOffset"></param>
        /// <param name="dataLength"></param>
        public async Task WriteAsync(long fileStartPoint, byte[] data, int dataOffset, int dataLength)
        {
            var task = new TaskCompletionSource<bool>();

            var fileSegment = new FileSegment(fileStartPoint, data, dataOffset, dataLength, task);
            DownloadSegmentList.Enqueue(fileSegment);
            await task.Task;
        }

        private Exception? Exception { set; get; }

        private async Task WriteToFile()
        {
            while (true)
            {
                var fileSegment = await DownloadSegmentList.DequeueAsync();

                try
                {
                    Stream.Seek(fileSegment.FileStartPoint, SeekOrigin.Begin);

                    await Stream.WriteAsync(fileSegment.Data, fileSegment.DataOffset, fileSegment.DataLength);
                }
                catch (Exception e)
                {
                    Exception = e;
                    WriteFinished?.Invoke(this, EventArgs.Empty);
                    return;
                }

                try
                {
                    fileSegment.TaskCompletionSource?.SetResult(true);
                }
                catch (Exception)
                {
                    // 执行业务代码
                }

                var isEmpty = DownloadSegmentList.Count == 0;

                if (isEmpty)
                {
                    WriteFinished?.Invoke(this, EventArgs.Empty);
                    if (_isDispose)
                    {
                        return;
                    }
                }
            }
        }

        private event EventHandler WriteFinished;

        private FileStream Stream { get; }

        private AsyncQueue<FileSegment> DownloadSegmentList { get; } = new AsyncQueue<FileSegment>();

        readonly struct FileSegment
        {
            public FileSegment(long fileStartPoint, byte[] data, int dataOffset, int dataLength, TaskCompletionSource<bool> taskCompletionSource = null)
            {
                FileStartPoint = fileStartPoint;
                Data = data;
                DataLength = dataLength;
                DataOffset = dataOffset;
                TaskCompletionSource = taskCompletionSource;
            }

            /// <summary>
            /// 文件开始写入的点
            /// </summary>
            public long FileStartPoint { get; }

            /// <summary>
            /// 表示从 <see cref="Data"/> 的读取点
            /// </summary>
            public int DataOffset { get; }

            /// <summary>
            /// 写入文件的数据
            /// </summary>
            public byte[] Data { get; }

            /// <summary>
            /// 表示从 <see cref="Data"/> 的读取长度
            /// </summary>
            public int DataLength { get; }

            public TaskCompletionSource<bool> TaskCompletionSource { get; }
        }

        /// <summary>
        /// 等待文件写入，文件写入完成时磁盘文件不一定写入完成
        /// </summary>
        /// <exception cref="IOException"></exception>
        /// <returns></returns>
        public async ValueTask DisposeAsync()
        {
            // 从业务上，这里只有一个线程访问，也不会有重入
            _isDispose = true;

            if (Exception != null)
            {
                ExceptionDispatchInfo.Capture(Exception).Throw();
            }
            // 需要先判断 Exception 因为加入只进入一个任务，刚好这个任务炸了
            if (DownloadSegmentList.Count == 0)
            {
                return;
            }

            var task = new TaskCompletionSource<bool>();

            WriteFinished += (sender, args) =>
            {
                if (Exception != null)
                {
                    task.SetException(Exception);
                }
                else
                {
                    task.SetResult(true);
                }
                WriteFinished = null;
            };

            if (Exception != null)
            {
                throw Exception;
            }

            if (DownloadSegmentList.Count == 0)
            {
                return;
            }

            await task.Task;
        }

        private bool _isDispose;
    }

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