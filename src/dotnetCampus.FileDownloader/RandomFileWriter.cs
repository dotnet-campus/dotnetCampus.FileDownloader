﻿using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using dotnetCampus.Threading;
using IAsyncDisposable = System.IAsyncDisposable;

namespace dotnetCampus.FileDownloader
{
    /// <summary>
    /// 不按照顺序，随机写入文件
    /// </summary>
    public class RandomFileWriter : IAsyncDisposable, IRandomFileWriter
    {
        /// <summary>
        /// 不按照顺序，随机写入文件
        /// </summary>
        /// <param name="stream"></param>
        public RandomFileWriter(FileStream stream)
        {
            Stream = stream;
            Task.Run(WriteToFile);
        }

        /// <summary>
        /// 加入写文件队列
        /// </summary>
        public void QueueWrite(long fileStartPoint, byte[] data, int dataOffset, int dataLength)
        {
            var fileSegment = new FileSegment(fileStartPoint, data, dataOffset, dataLength);
            FileSegmentList.Enqueue(fileSegment);
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
            FileSegmentList.Enqueue(fileSegment);
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
            FileSegmentList.Enqueue(fileSegment);
            await task.Task;
        }

        /// <summary>
        /// 每次写完触发事件
        /// </summary>
        public event EventHandler<StepWriteFinishedArgs> StepWriteFinished = delegate { };

        private Exception? Exception { set; get; }

        private bool _isWriting;

        private async Task WriteToFile()
        {
            while (true)
            {
                _isWriting = true;
                var fileSegment = await FileSegmentList.DequeueAsync();

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

                _isWriting = false;

                try
                {
                    fileSegment.TaskCompletionSource?.SetResult(true);

                    StepWriteFinished
                    (
                        this,
                        new StepWriteFinishedArgs
                        (
                            fileSegment.FileStartPoint,
                            fileSegment.DataOffset,
                            fileSegment.Data,
                            fileSegment.DataLength
                        )
                    );
                }
                catch (Exception)
                {
                    // 执行业务代码
                }

                var isEmpty = FileSegmentList.Count == 0;

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

        private event EventHandler? WriteFinished;

        private FileStream Stream { get; }

        private AsyncQueue<FileSegment> FileSegmentList { get; } = new AsyncQueue<FileSegment>();

        private readonly struct FileSegment
        {
            public FileSegment(long fileStartPoint, byte[] data, int dataOffset, int dataLength,
                TaskCompletionSource<bool>? taskCompletionSource = null)
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

            public TaskCompletionSource<bool>? TaskCompletionSource { get; }
        }

        /// <summary>
        /// 等待文件写入，文件写入完成时磁盘文件不一定写入完成
        /// </summary>
        /// <exception cref="IOException"></exception>
        /// <returns></returns>
        public async ValueTask DisposeAsync()
        {
            try
            {
                // 从业务上，这里只有一个线程访问，也不会有重入
                _isDispose = true;

                if (Exception != null)
                {
                    ExceptionDispatchInfo.Capture(Exception).Throw();
                }

                // 需要先判断 Exception 因为加入只进入一个任务，刚好这个任务炸了
                if (FileSegmentList.Count == 0)
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
                    ExceptionDispatchInfo.Capture(Exception).Throw();
                }

                // 这个判断存在一个坑，也就是在写入出队的时候，此时其实还没有实际写完成
                // 在 var fileSegment = await FileSegmentList.DequeueAsync() 方法出队了
                // 但是 Stream.WriteAsync 还没完成
                if (FileSegmentList.Count == 0 && !_isWriting)
                {
                    return;
                }

                await task.Task;
            }
            finally
            {
                await FileSegmentList.DisposeAsync();
            }
        }

        private bool _isDispose;
    }
}
