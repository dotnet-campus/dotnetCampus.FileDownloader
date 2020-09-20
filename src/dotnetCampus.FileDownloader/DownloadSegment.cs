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

        /// <summary>
        /// 创建下载的段
        /// </summary>
        public DownloadSegment()
        {
            StartPoint = 0;
        }

        /// <summary>
        /// 创建下载的段
        /// </summary>
        /// <param name="startPoint"></param>
        /// <param name="requirementDownloadPoint"></param>
        public DownloadSegment(long startPoint, long requirementDownloadPoint)
        {
            StartPoint = startPoint;
            _requirementDownloadPoint = requirementDownloadPoint;
        }

        /// <summary>
        /// 下载起始点
        /// </summary>
        public long StartPoint { get; }

        /// <summary>
        /// 当前段的序号，仅用于调试
        /// </summary>
        public int Number { get; set; }

        /// <summary>
        /// 当前的信息，仅用于调试
        /// </summary>
        public string? Message { get; set; }

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

        /// <inheritdoc />
        public override string ToString()
        {
            return $"[{Number:00}] Progress {DownloadedLength * 100.0 / (RequirementDownloadPoint - StartPoint):0.00} Start={StartPoint} Require={RequirementDownloadPoint} Download={DownloadedLength}/{RequirementDownloadPoint - StartPoint} {Message}";
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

        /// <summary>
        /// 是否下载完成
        /// </summary>
        public bool Finished => CurrentDownloadPoint >= RequirementDownloadPoint;

        /// <summary>
        /// 分段管理
        /// </summary>
        public SegmentManager? SegmentManager { set; get; }
    }
}