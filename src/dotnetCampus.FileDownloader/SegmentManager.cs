using System;
using System.Collections.Generic;
using System.Linq;

namespace dotnetCampus.FileDownloader
{
    /// <summary>
    /// 文件分段管理
    /// </summary>
    public class SegmentManager
    {
        /// <summary>
        /// 创建文件分段管理
        /// </summary>
        /// <param name="fileLength">文件长度</param>
        public SegmentManager(long fileLength)
        {
            FileLength = fileLength;
        }

        /// <summary>
        /// 创建文件分段管理
        /// </summary>
        public SegmentManager(List<DownloadSegment> downloadSegmentList)
        {
            DownloadSegmentList.AddRange(downloadSegmentList);
        }

        /// <summary>
        /// 下载文件长度
        /// </summary>
        public long FileLength { get; }

        /// <summary>
        /// 创建一个新的分段用于下载
        /// </summary>
        public DownloadSegment? GetNewDownloadSegment()
        {
            lock (_locker)
            {
                var downloadSegment = NewDownloadSegment();

                if (downloadSegment == null)
                {
                    return null;
                }

                RegisterDownloadSegment(downloadSegment);

                return downloadSegment;
            }
        }

        /// <summary>
        /// 是否下载完成
        /// </summary>
        /// <returns></returns>
        public bool IsFinished()
        {
            lock (_locker)
            {
                return DownloadSegmentList.TrueForAll(segment => segment.Finished);
            }
        }

        internal (DownloadSegment? segment, int runCount, TimeSpan maxReportTime) GetDownloadSegmentStatus()
        {
            lock (_locker)
            {
                int maxCount = DownloadSegmentList.Count;
                TimeSpan maxReportTime = TimeSpan.MinValue;
                int runCount = 0;
                DownloadSegment? segment = null;
                for (int i = 0; i < maxCount; i++)
                {
                    DownloadSegment downloadSegment = DownloadSegmentList[i];
                    if (downloadSegment.LoadingState == DownloadingState.Runing)
                    {
                        var reportTime = (DateTime.Now - downloadSegment.LastDownTime);
                        if (reportTime >= maxReportTime)
                        {
                            maxReportTime = reportTime;
                            segment = downloadSegment;
                        }
                        runCount++;
                    }
                }

                return (segment, runCount, maxReportTime);
            }
        }

        private DownloadSegment? NewDownloadSegment()
        {
            if (DownloadSegmentList.Count == 0)
            {
                return new DownloadSegment(startPoint: 0, requirementDownloadPoint: FileLength);
            }

            // 此时需要拿到当前最大的空段是哪一段

            long emptySegmentLength = 0;
            var previousSegmentIndex = -1;

            for (var i = 0; i < DownloadSegmentList.Count - 1; i++)
            {
                var segment = DownloadSegmentList[i];
                var nextSegment = DownloadSegmentList[i + 1];

                var emptyLength = nextSegment.StartPoint - segment.CurrentDownloadPoint;
                if (emptyLength > emptySegmentLength)
                {
                    emptySegmentLength = emptyLength;
                    previousSegmentIndex = i;
                }
            }

            // 最后一段
            var lastDownloadSegmentIndex = DownloadSegmentList.Count - 1;
            var lastDownloadSegment = DownloadSegmentList[lastDownloadSegmentIndex];
            var lastDownloadSegmentEmptyLength = FileLength - lastDownloadSegment.CurrentDownloadPoint;
            if (lastDownloadSegmentEmptyLength > emptySegmentLength)
            {
                emptySegmentLength = lastDownloadSegmentEmptyLength;
                previousSegmentIndex = lastDownloadSegmentIndex;
            }

            if (previousSegmentIndex >= 0)
            {
                long requirementDownloadPoint;

                var previousDownloadSegment = DownloadSegmentList[previousSegmentIndex];
                long currentDownloadPoint = previousDownloadSegment.CurrentDownloadPoint;

                if (previousSegmentIndex == lastDownloadSegmentIndex)
                {
                    requirementDownloadPoint = FileLength;
                }
                else
                {
                    var nextDownloadSegment = DownloadSegmentList[previousSegmentIndex + 1];
                    requirementDownloadPoint = nextDownloadSegment.StartPoint;
                }

                var length = emptySegmentLength;
                var center = (length / 2) + currentDownloadPoint;

                previousDownloadSegment.RequirementDownloadPoint = center;
                return new DownloadSegment(center, requirementDownloadPoint);
            }

            return null;
        }

        /// <summary>
        /// 注册下载段
        /// </summary>
        /// <param name="downloadSegment"></param>
        public void RegisterDownloadSegment(DownloadSegment downloadSegment)
        {
            downloadSegment.Message = "Start RegisterDownloadSegment";
            lock (_locker)
            {
                // 找到顺序
                var n = DownloadSegmentList.FindIndex(temp => temp.StartPoint > downloadSegment.StartPoint);
                if (n < 0)
                {
                    // 找不到一个比他大的，放在最后面
                    DownloadSegmentList.Add(downloadSegment);
                }
                else
                {
                    // 原本是按照顺序的，找到第一个比他大的，放在前面
                    DownloadSegmentList.Insert(n, downloadSegment);
                }

                downloadSegment.Number = DownloadSegmentList.Count;

                downloadSegment.SegmentManager = this;
            }

            downloadSegment.Message = "Finish RegisterDownloadSegment";
        }

        /// <summary>
        /// 获取当前所有下载段
        /// </summary>
        /// <returns></returns>
        public IReadOnlyList<DownloadSegment> GetCurrentDownloadSegmentList()
        {
            lock (_locker)
            {
                return DownloadSegmentList.ToList();
            }
        }

        /// <summary>
        /// 获取下载完成的文件长度
        /// </summary>
        /// <returns></returns>
        public long GetDownloadedLength()
        {
            lock (_locker)
            {
                return DownloadSegmentList.Sum(downloadSegment => downloadSegment.DownloadedLength);
            }
        }

        private List<DownloadSegment> DownloadSegmentList { get; } = new List<DownloadSegment>();
        private readonly object _locker = new object();
    }
}
