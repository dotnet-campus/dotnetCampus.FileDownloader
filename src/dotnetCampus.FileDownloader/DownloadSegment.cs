using System;

namespace dotnetCampus.FileDownloader
{
    /// <summary>
    /// 下载的段，这个段的内容和长度将会不断更改
    /// </summary>
    public class DownloadSegment
    {
        private long _downloadedLength;
        private long _requirementDownloadPoint;

        /// <summary>
        /// 下载管理在发现支持分段下载的时候给出事件
        /// </summary>
        public event EventHandler SegmentChanged = delegate { };

        public DownloadSegment()
        {
            StartPoint = 0;
        }

        public DownloadSegment(long startPoint, long requirementDownloadPoint)
        {
            StartPoint = startPoint;
            _requirementDownloadPoint = requirementDownloadPoint;
        }

        public long StartPoint { get; }

        public int Number { get; set; }

        public string? Message { get; set; }
        public DateTime LastDownTime { get; set; } = DateTime.Now;
        public LoadingState LoadingState { get; set; } = LoadingState.Pause;
        /// <summary>
        /// 需要下载到的点
        /// </summary>
        public long RequirementDownloadPoint
        {
            internal set
            {
                _requirementDownloadPoint = value;
                SegmentChanged?.Invoke(this, EventArgs.Empty);
            }
            get => _requirementDownloadPoint;
        }

        public override string ToString()
        {
            return $"[{Number:00}] Progress {DownloadedLength * 100.0 / (RequirementDownloadPoint - StartPoint):0.00} Start={StartPoint} Require={RequirementDownloadPoint} Download={DownloadedLength}/{RequirementDownloadPoint - StartPoint} {Message} Time={(DateTime.Now - LastDownTime).TotalMilliseconds}";
        }

        /// <summary>
        /// 已经下载的长度
        /// </summary>
        /// 下载的时候需要通告管理器
        public long DownloadedLength
        {
            get => _downloadedLength;
            internal set
            {
                _downloadedLength = value;
            }
        }

        /// <summary>
        /// 当前的下载点
        /// </summary>
        /// 需要处理多线程访问
        public long CurrentDownloadPoint => StartPoint + DownloadedLength;

        public bool Finished => CurrentDownloadPoint >= RequirementDownloadPoint;

        /// <summary>
        /// 分段管理
        /// </summary>
        public SegmentManager? SegmentManager { set; get; }
    }
    /// <summary>
    /// 下载状态
    /// </summary>
    public enum LoadingState
    {
        Runing = 1,
        Pause = 0,
        Stop = -1
    }
}