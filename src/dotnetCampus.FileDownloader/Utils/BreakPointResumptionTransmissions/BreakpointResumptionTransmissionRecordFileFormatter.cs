﻿using System;
using System.Collections.Generic;
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
        // 设计上刚好可以复用 buffer 的值去进行读取
        var buffer = new byte[sizeof(long)];
        (bool success,long data) = Read();
        if(!success || data != header)
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

        List<(long startPoint, long length)> downloadedInfo = new();

        // 后续的信息就需要循环读取
        while (success)
        {
            (success, data) = Read();
            if(!success)
            {
                // 读取完成
                break;
            }
            if(data != (long) DataType.DownloadedInfo)
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
            downloadedInfo.Add((startPoint, length));
        }

        return new BreakPointResumptionTransmissionInfo(downloadLength, downloadedInfo);

        (bool success,long data) Read()
        {
           var readCount = stream.Read(buffer,0,buffer.Length);
            if (readCount != buffer.Length)
            {
                return (false,default(long));
            }

            var data = BitConverter.ToInt64(buffer,0);
            return (true, data);
        }
    }

    public void Write(Stream stream, BreakPointResumptionTransmissionInfo info)
    {
        var header = GetHeader();

    }

    private long GetHeader()
    {
        // 文件头是 dotnet campus File Downloader BreakPointResumptionTransmissionInfo 几个单词的首个字符 DCFBPRTI 缩写的 ASCII 值
        //var headerByteList = System.Text.Encoding.ASCII.GetBytes("DCFBPRTI");
        // var headerByteList = new byte[] { 68, 67, 70, 66, 80, 82, 84, 73 };
        //return BitConverter.ToInt64(headerByteList)
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