using System;
using System.Collections.Generic;

namespace dotnetCampus.FileDownloader
{
    /// <summary>
    /// 下载进度
    /// </summary>
    public class DownloadProgress
    {
        public DownloadProgress(SegmentManager segmentManager)
        {
            SegmentManager = segmentManager;
        }

        public DownloadProgress(string message, SegmentManager segmentManager)
        {
            Message = message;
            SegmentManager = segmentManager;
        }

        public string Message { get; } = "";

        public long DownloadedLength => SegmentManager.GetDownloadedLength();

        public long FileLength => SegmentManager.FileLength;

        private SegmentManager SegmentManager { get; }

        /// <summary>
        /// 获取当前所有下载段
        /// </summary>
        /// <returns></returns>
        public IReadOnlyList<DownloadSegment> GetCurrentDownloadSegmentList()
        {
            return SegmentManager.GetCurrentDownloadSegmentList();
        }
    }
}
