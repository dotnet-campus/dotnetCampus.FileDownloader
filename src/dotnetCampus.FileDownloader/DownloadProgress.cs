using System;
using System.Collections.Generic;

namespace dotnetCampus.FileDownloader
{
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

        public IReadOnlyList<DownloadSegment> GetDownloadSegmentList()
        {
            return SegmentManager.GetDownloadSegmentList();
        }
    }
}