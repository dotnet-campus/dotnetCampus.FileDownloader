#if NETCOREAPP3_1_OR_GREATER

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace dotnetCampus.FileDownloader.Utils.BreakpointResumptionTransmissions;

/// <summary>
/// 断点续传管理
/// </summary>
internal partial class BreakpointResumptionTransmissionManager : IDisposable
{
    public BreakpointResumptionTransmissionManager(FileInfo breakpointResumptionTransmissionRecordFile, IRandomFileWriter fileWriter, ISharedArrayPool sharedArrayPool, long contentLength, int bufferLength = ushort.MaxValue)
    {
        BreakpointResumptionTransmissionRecordFile = breakpointResumptionTransmissionRecordFile;
        DownloadLength = contentLength;
        BufferLength = bufferLength;
        SharedArrayPool = sharedArrayPool;

        fileWriter.StepWriteFinished += (sender, args) => RecordDownloaded(args);
    }

    public ISharedArrayPool SharedArrayPool { get; }

    public FileInfo BreakpointResumptionTransmissionRecordFile { get; }

    /// <summary>
    /// 下载的长度
    /// </summary>
    public long DownloadLength { get; }

    public int BufferLength { get; }

    /// <summary>
    /// 创建分段下载数据
    /// </summary>
    /// <param name="downloadFileStream">正在被下载的文件的 FileStream 内容，用于断点续传校验内容</param>
    /// <returns></returns>
    public async Task<SegmentManager> CreateSegmentManagerAsync(FileStream downloadFileStream)
    {
        // 如果存在断点续传记录文件，那将从此文件读取断点续传信息
        // 如果读取的信息有误，或者是校验失败等
        // 那就重新下载

        // 还没有准备去释放
        FileStream = new FileStream(BreakpointResumptionTransmissionRecordFile.FullName, FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.Read, bufferSize: 4096,
            // 配置 WriteThrough 要求立即写入磁盘
            FileOptions.WriteThrough);
        BinaryWriter = new BinaryWriter(FileStream);

        // 进行一些初始化逻辑
        Formatter = new BreakpointResumptionTransmissionRecordFileFormatter();

        var info = await Formatter.ReadAsync(FileStream);

        if (info is not null && info.DownloadLength == DownloadLength && info.DownloadedInfo is not null && info.DownloadedInfo.Count > 0)
        {
            var downloadSegmentList = await GetDownloadSegmentList(info.DownloadedInfo, downloadFileStream);

            var segmentManager = new SegmentManager(downloadSegmentList);
            for (var i = 0; i < downloadSegmentList.Count; i++)
            {
                var item = downloadSegmentList[i];

#if DEBUG
                // 以下用来测试是否从断点续传生成的数据是正确的
                if (i < downloadSegmentList.Count - 1)
                {
                    var next = downloadSegmentList[i + 1];
                    // 当前的下载到的最后一个点，需要等于下一段的起始。否则将会存在一段没有下载
                    var lastPoint = item.RequirementDownloadPoint;
                    if (lastPoint != next.StartPoint)
                    {
                        // 证明有锅，存在一段没有被下载
                        Debugger.Break();
                    }
                }
                else
                {
                    // 最后一段应该下载的点等于下载长度
                    var lastPoint = item.RequirementDownloadPoint;
                    if (lastPoint != DownloadLength)
                    {
                        // 证明有锅，最后一段没有下载
                        Debugger.Break();
                    }
                }
#endif
                item.SegmentManager = segmentManager;
                item.Number = i;
            }

            return segmentManager;
        }
        else
        {
            // 如果读取失败，文件没有创建，读取到的下载长度不对
            // 那就忽略此断点续传文件
            // 清空原有的文件，如果原来是没有创建的文件的，那本来就是 0 的值
            FileStream.SetLength(0);
            Formatter.Write(BinaryWriter, new BreakpointResumptionTransmissionInfo(DownloadLength));
            return new SegmentManager(DownloadLength);
        }
    }

    /// <summary>
    /// 通过断点续传的信息获取下载的内容
    /// </summary>
    /// <param name="downloadedInfo"></param>
    /// <param name="downloadFileStream"></param>
    /// <returns></returns>
    private async Task<List<DownloadSegment>> GetDownloadSegmentList(List<DataRange> downloadedInfo, FileStream downloadFileStream)
    {
        downloadedInfo.Sort(new DataRangeComparer());
        var list = downloadedInfo;

        var downloadSegmentList = new List<DownloadSegment>();
        for (var i = 0; i < list.Count; i++)
        {
            DataRange current = list[i];

            if (i == 0)
            {
                // 第零个要处理距离开始的距离
                var length = current.StartPoint - 0;
                if (length == 0)
                {
                    // 证明第零处理了，啥都不用做
                }
                else
                {
                    // 还没下载第零加入下载
                    var startPoint = 0;
                    downloadSegmentList.Add(new DownloadSegment(startPoint, startPoint + length));
                }
            }

            // 需要对原文件进行校验，确保下载下去的和断点续传记录的相同
            async Task<bool> IsDownloaded()
            {
                var endPoint = current.StartPoint + current.Length;
                if (downloadFileStream.Length < endPoint)
                {
                    return false;
                }

                downloadFileStream.Seek(current.StartPoint, SeekOrigin.Begin);

                return await CrcHelper.CheckCrcAsync(downloadFileStream, current.Checksum, current.Length, SharedArrayPool,
                     BufferLength);
            }

            // 如果判断当前不是下载完成的内容，则配置状态不是 Finished 而是需要下载 
            var isDownloaded = await IsDownloaded();

            var currentDownloadSegment = new DownloadSegment(current.StartPoint, current.StartPoint + current.Length)
            {
                // 已经下载的长度。如果校验下载失败，则长度是 0 否则为记录的长度
                DownloadedLength = isDownloaded ? current.Length : 0,
                LoadingState = isDownloaded ? DownloadingState.Finished : DownloadingState.Pause,
            };
            downloadSegmentList.Add(currentDownloadSegment);

            if (i != list.Count - 1)
            {
                // 不是最后一段之前，需要处理段之间的需要下载内容
                var next = list[i + 1];
                // 求当前和下一段之间是否有需要下载的
                var length = next.StartPoint - current.LastPoint;

                if (length <= 0)
                {
                    // 证明这两段是连续的，啥都不用做
                    // 如果小于零，证明两段有重叠，这是可以支持的，也就是存在有一段由于多线程原因，重复下载两次
                }
                else
                {
                    downloadSegmentList.Add(new DownloadSegment(current.LastPoint, next.StartPoint));
                }
            }
            else //if (i == list.Count - 1)
            {
                // 最后一段需要处理和下载长度的距离
                var length = DownloadLength - current.LastPoint;
                if (length <= 0)
                {
                    // 证明下载到最后
                    // 如果小于零，那只能证明给定的断点续传数据有毒
                }
                else
                {
                    Debug.Assert(current.LastPoint + length == DownloadLength);
                    downloadSegmentList.Add(new DownloadSegment(current.LastPoint, current.LastPoint + length));
                }
            }
        }

        return downloadSegmentList;
    }

    class DataRangeComparer : IComparer<DataRange>
    {
        public int Compare(DataRange x, DataRange y)
        {
            return x.StartPoint.CompareTo(y.StartPoint);
        }
    }

    private BreakpointResumptionTransmissionRecordFileFormatter? Formatter { set; get; }
    private FileStream? FileStream { set; get; }
    private BinaryWriter? BinaryWriter { set; get; }

    /// <summary>
    /// 记录已下载数据
    /// </summary>
    /// <param name="args"></param>
    /// <exception cref="NotImplementedException"></exception>
    /// <remarks>理论上每次只有单个线程可以进入，刚好是写入文件的线程才能访问此方法</remarks>
    private void RecordDownloaded(StepWriteFinishedArgs args)
    {
        if (_isDisposed)
        {
            return;
        }

        if (Formatter is null || FileStream is null || BinaryWriter is null)
        {
            throw new InvalidOperationException("必须在调用 CreateSegmentManagerAsync 完成之后才能进入 RecordDownloaded 方法");
        }

        var crc32 = new Crc64();
        var checksum = crc32.Append(args.Data.AsSpan(args.DataOffset, args.DataLength));

        Formatter.AppendDataRange(BinaryWriter, new DataRange(args.FileStartPoint, args.DataLength, checksum));
    }

    public void Dispose()
    {
        _isDisposed = true;
        FileStream?.Dispose();
        FileStream = null;
        BinaryWriter = null;
    }

    private bool _isDisposed;
}
#endif
