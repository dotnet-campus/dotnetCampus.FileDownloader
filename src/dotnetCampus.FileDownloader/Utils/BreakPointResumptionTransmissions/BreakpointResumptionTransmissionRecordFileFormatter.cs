using System;
using System.IO;

namespace dotnetCampus.FileDownloader.Utils.BreakPointResumptionTransmissionManager;

/// <summary>
/// 断点续传记录文件格式化器
/// </summary>
/// 文件格式：【文件头】【下载文件的下载长度】【各个已下载的数据段信息】
class BreakpointResumptionTransmissionRecordFileFormatter
{
    public BreakPointResumptionTransmissionInfo? Read(Stream stream)
    {
        var header = GetHeader();
        var buffer = new byte[header.Length];
        var readCount = stream.Read(buffer, 0, buffer.Length);
        if (readCount < header.Length)
        {
            // 如果读取不到 Header 的长度的内容，那返回空即可，让上层业务处理
            return null;
        }

        for (int i = 0; i < header.Length; i++)
        {
            // 如果有任何和 Header 不相同的，返回空即可，证明此记录内容不对
            if (header[i] != buffer[i])
            {
                return null;
            }
        }

        // 预期在 Header 之后是下载文件的长度
        BinaryReader binaryReader = new BinaryReader(stream);
        var dataType = binaryReader.ReadInt64();
        if(dataType != (long) DataType.DownloadFileLength)
        {
            // 文件组织形式出错，返回空即可
            return null;
        }

        // 获取需要下载的文件长度
        var downloadLength = binaryReader.ReadInt64();

        static (bool success,long data) Read(Stream stream)
        {
            //BitConverter.ToInt64
        }
    }

    public void Write(Stream stream, BreakPointResumptionTransmissionInfo info)
    {
        var header = GetHeader();

    }

    private byte[] GetHeader()
    {
        // 文件头是 dotnet campus File Downloader BreakPointResumptionTransmissionInfo 几个单词的首个字符 DCFB 缩写的 ASCII 值
        return new byte[] { 68, 67, 70, 66 };
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
