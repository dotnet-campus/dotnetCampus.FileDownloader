using System;

namespace dotnetCampus.FileDownloader
{
    /// <summary>
    /// 不按照顺序，随机写入文件
    /// </summary>
    public interface IRandomFileWriter : IAsyncDisposable
    {
        /// <summary>
        /// 加入写文件队列
        /// </summary>
        void QueueWrite(long fileStartPoint, byte[] data, int dataOffset, int dataLength);

        /// <summary>
        /// 每次写完触发事件
        /// </summary>
        event EventHandler<StepWriteFinishedArgs> StepWriteFinished;
    }
}