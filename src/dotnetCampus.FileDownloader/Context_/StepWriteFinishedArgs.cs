using System;

namespace dotnetCampus.FileDownloader
{
    /// <summary>
    /// 每一步写入完成的事件参数
    /// </summary>
    public class StepWriteFinishedArgs : EventArgs
    {
        /// <summary>
        /// 每一步写入完成的事件参数
        /// </summary>
        /// <param name="fileStartPoint"></param>
        /// <param name="dataOffset"></param>
        /// <param name="data"></param>
        /// <param name="dataLength"></param>
        public StepWriteFinishedArgs(long fileStartPoint, int dataOffset, byte[] data, int dataLength)
        {
            FileStartPoint = fileStartPoint;
            DataOffset = dataOffset;
            Data = data;
            DataLength = dataLength;
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
}
