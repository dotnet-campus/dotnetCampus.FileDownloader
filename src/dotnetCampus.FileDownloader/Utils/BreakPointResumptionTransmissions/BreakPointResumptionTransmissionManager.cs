using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotnetCampus.FileDownloader.Utils.BreakPointResumptionTransmissionManager;

/// <summary>
/// 断点续传管理
/// </summary>
internal class BreakPointResumptionTransmissionManager
{
    public BreakPointResumptionTransmissionManager(FileInfo breakpointResumptionTransmissionRecordFile, IRandomFileWriter fileWriter, long contentLength)
    {
        BreakpointResumptionTransmissionRecordFile = breakpointResumptionTransmissionRecordFile;
        DownloadLength = contentLength;

        fileWriter.StepWriteFinished += (sender, args) => RecordDownloaded(args);
    }

    public FileInfo BreakpointResumptionTransmissionRecordFile { get; }

    /// <summary>
    /// 下载的长度
    /// </summary>
    public long DownloadLength { get; }

    /// <summary>
    /// 创建分段下载数据
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public SegmentManager CreateSegmentManager()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 记录已下载数据
    /// </summary>
    /// <param name="args"></param>
    /// <exception cref="NotImplementedException"></exception>
    /// <remarks>理论上每次只有单个线程可以进入，刚好是写入文件的线程才能访问此方法</remarks>
    private void RecordDownloaded(StepWriteFinishedArgs args)
    {
        // 如果全部下载完成了？


        throw new NotImplementedException();
    }
}
