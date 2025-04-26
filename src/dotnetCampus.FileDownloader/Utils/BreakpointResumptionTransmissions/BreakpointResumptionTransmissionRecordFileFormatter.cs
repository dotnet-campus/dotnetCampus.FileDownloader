using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace dotnetCampus.FileDownloader.Utils.BreakpointResumptionTransmissions;

/// <summary>
/// 断点续传记录文件格式化器
/// </summary>
/// 文件格式：【文件头】【下载文件的下载长度】【各个已下载的数据段信息】
class BreakpointResumptionTransmissionRecordFileFormatter
{
    public BreakpointResumptionTransmissionInfo? Read(Stream stream)
    {
        var header = GetHeader();
        // 设计上刚好可以复用 buffer 的值去进行读取
        var buffer = new byte[sizeof(long)];
        (var success, var data) = Read();
        if (!success || data != header)
        {
            // 如果读取不到 Header 的长度的内容，那返回空即可，让上层业务处理
            // 如果有任何和 Header 不相同的，返回空即可，证明此记录内容不对
            return null;
        }

        // 预期在 Header 之后是下载文件的长度
        (success, data) = Read();
        if (!success || data != (long) DataType.DownloadFileLength)
        {
            // 证明文件组织形式错误了，没有读取到下载文件的长度
            return null;
        }

        // 获取需要下载的文件长度
        (success, data) = Read();
        if (!success)
        {
            // 没有读取到下载的文件长度，返回空即可
            return null;
        }
        var downloadLength = data;

        List<DataRange> downloadedInfo = new();

        // 后续的信息就需要循环读取
        while (success)
        {
            // 后续的信息一个信息由三个 Int64 组成
            // 第一个是 DataType
            // 第二个是 起始点
            // 第三个是 长度
            // 每段下载完成写入文件，将会记录写入的起始点和长度，通过起始点和长度 的列表可以算出当前还有哪些内容还没下载完成。如此即可实现断点续传功能
            (success, data) = Read();
            if (!success)
            {
                // 读取完成
                break;
            }
            if (data != (long) DataType.DownloadedInfo)
            {
                // 记录里面包含错误的数据，立刻返回
                // 如果在有错误的数据情况下，还不重新建立记录文件，那将会导致后续下载记录的内容被无效
                return null;
            }

            (success, data) = Read();
            if (!success)
            {
                // 数据错误，没有记录全一条信息，重新建立记录文件
                return null;
            }

            var startPoint = data;
            (success, data) = Read();
            if (!success)
            {
                // 数据错误，没有记录全一条信息，重新建立记录文件
                return null;
            }
            var length = data;
            downloadedInfo.Add(new DataRange(startPoint, length));
        }

        return new BreakpointResumptionTransmissionInfo(downloadLength, downloadedInfo);

        (bool success, long data) Read()
        {
            // 用于调试读取失败时，读取到哪个内容
            var originPosition = stream.Position;
            _ = originPosition;

            var readCount = stream.Read(buffer, 0, buffer.Length);
            if (readCount != buffer.Length)
            {
                return (false, default(long));
            }

            var data = BitConverter.ToInt64(buffer, 0);
            return (true, data);
        }
    }

    public void Write(BinaryWriter binaryWriter, BreakpointResumptionTransmissionInfo info)
    {
        var header = GetHeader();
        binaryWriter.Write(header);

        // 写入下载的文件长度，用于下次下载时，判断文件下载的长度不对，可以炸掉
        binaryWriter.Write((long) DataType.DownloadFileLength);
        binaryWriter.Write(info.DownloadLength);

        if (info.DownloadedInfo is not null)
        {
            // 预期是不会进入这里，因此代码先不写
            foreach (var downloadedInfo in info.DownloadedInfo)
            {
                AppendDataRange(binaryWriter, downloadedInfo);
            }
        }
    }

    public void AppendDataRange(BinaryWriter binaryWriter, DataRange dataRange)
    {
        binaryWriter.Write((long) DataType.DownloadedInfo);
        binaryWriter.Write(dataRange.StartPoint);
        binaryWriter.Write(dataRange.Length);
    }

    private static long GetHeader()
    {
        // 文件头是 dotnet campus File Downloader BreakpointResumptionTransmissionInfo 几个单词的首个字符 DCFBPRTI 缩写的 ASCII 值
        // 刚好将这个 ASCII 的 byte 数组转换为一个 long 的值
        //var headerByteList = System.Text.Encoding.ASCII.GetBytes("DCFBPRTI");
        // var headerByteList = new byte[] { 68, 67, 70, 66, 80, 82, 84, 73 };
        //return BitConverter.ToInt64(headerByteList)
        // 由于这个类还想支持 NET45 等，就不用 MemoryMarshal 了
        //return MemoryMarshal.Read<long>("DCFBPRTI"u8);
        return 5283938767475196740;
    }

    enum DataType : long
    {
        /// <summary>
        /// 所需下载的文件长度
        /// </summary>
        DownloadFileLength = 0x1,

        /// <summary>
        /// 已下载的信息，包括起点和长度
        /// </summary>
        DownloadedInfo = 0x2,
    }
}
