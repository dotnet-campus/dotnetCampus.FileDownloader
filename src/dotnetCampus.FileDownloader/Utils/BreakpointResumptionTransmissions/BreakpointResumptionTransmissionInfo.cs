using System.Collections.Generic;

namespace dotnetCampus.FileDownloader.Utils.BreakpointResumptionTransmissions;

/// <summary>
/// 断点续传信息
/// </summary>
class BreakpointResumptionTransmissionInfo
{
    public BreakpointResumptionTransmissionInfo(long downloadLength, List<DataRange>? downloadedInfo = null)
    {
        DownloadLength = downloadLength;
        DownloadedInfo = downloadedInfo;
    }

    /// <summary>
    /// 下载的长度
    /// </summary>
    public long DownloadLength { get; }

    /// <summary>
    /// 已经下载的信息
    /// </summary>
    public List<DataRange>? DownloadedInfo { get; }
}
