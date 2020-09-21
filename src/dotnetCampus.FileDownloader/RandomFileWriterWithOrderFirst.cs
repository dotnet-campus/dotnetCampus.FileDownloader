using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using dotnetCampus.Threading;

namespace dotnetCampus.FileDownloader
{
    /// <summary>
    /// 顺序写入优先的支持乱序多线程的文件写入方法
    /// </summary>
    /// 和 <see cref="RandomFileWriter"/> 不同的是，这个方法采用让文件写入时，尽可能是连续写入，这样磁盘写入性能比较快，在下载速度比写入速度快的时候
    /// 这个方法可以做到比 <see cref="RandomFileWriter"/> 提升更多的速度
    public class RandomFileWriterWithOrderFirst : IRandomFileWriter
    {
        /// <summary>
        /// 创建文件写入方法
        /// </summary>
        /// <param name="stream"></param>
        public RandomFileWriterWithOrderFirst(FileStream stream)
        {
            Stream = stream;

            FileSegmentTaskList = new DoubleBufferTask<FileSegment>(WriteInner);
        }

        private FileStream Stream { get; }

        /// <summary>
        /// 每次写完触发事件
        /// </summary>
        public event EventHandler<StepWriteFinishedArgs> StepWriteFinished = delegate { };

        /// <inheritdoc />
        public void QueueWrite(long fileStartPoint, byte[] data, int dataOffset, int dataLength)
        {
            var fileSegment = new FileSegment(fileStartPoint, data, dataOffset, dataLength);
            FileSegmentTaskList.AddTask(fileSegment);
        }

        private DoubleBufferTask<FileSegment> FileSegmentTaskList { get; }

        private async Task WriteInner(List<FileSegment> fileSegmentList)
        {
            foreach (var fileSegment in GetFileSegment(fileSegmentList))
            {
                if (Stream.Position != fileSegment.FileStartPoint)
                {
                    Stream.Seek(fileSegment.FileStartPoint, SeekOrigin.Begin);
                }

                await Stream.WriteAsync(fileSegment.Data, fileSegment.DataOffset, fileSegment.DataLength);

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

            // 这个方法可以返回尽可能连续的数据，用于让文件可以连续写入，提升磁盘性能
            static IEnumerable<FileSegment> GetFileSegment(List<FileSegment> fileSegmentList)
            {
                long lastPosition = -1;
                while (fileSegmentList.Count != 0)
                {
                    if (lastPosition == -1)
                    {
                        var first = fileSegmentList[0];
                        fileSegmentList.RemoveAt(0);

                        lastPosition = first.FileStartPoint + first.DataLength;

                        yield return first;
                    }

                    bool canFound = false;
                    for (var i = 0; i < fileSegmentList.Count; i++)
                    {
                        var fileSegment = fileSegmentList[i];
                        if (fileSegment.FileStartPoint == lastPosition)
                        {
                            fileSegmentList.RemoveAt(i);
                            canFound = true;

                            lastPosition = fileSegment.FileStartPoint + fileSegment.DataLength;

                            yield return fileSegment;
                            break;
                        }
                    }

                    if (!canFound)
                    {
                        lastPosition = -1;
                    }
                }
            }
        }

        private readonly struct FileSegment
        {
            public FileSegment(long fileStartPoint, byte[] data, int dataOffset, int dataLength)
            {
                FileStartPoint = fileStartPoint;
                Data = data;
                DataLength = dataLength;
                DataOffset = dataOffset;
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
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            FileSegmentTaskList.Finish();

            await FileSegmentTaskList.WaitAllTaskFinish();
        }
    }
}