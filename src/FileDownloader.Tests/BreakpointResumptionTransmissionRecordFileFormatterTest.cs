﻿using System.Collections.Generic;
using System.IO;

using dotnetCampus.FileDownloader.Utils.BreakpointResumptionTransmissions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using MSTest.Extensions.Contracts;

namespace FileDownloader.Tests
{
    [TestClass]
    public class BreakpointResumptionTransmissionRecordFileFormatterTest
    {
        [ContractTestCase]
        public void Format()
        {
            "写入断点续传信息之后，可以读取数据".Test(async () =>
            {
                var formatter = new BreakpointResumptionTransmissionRecordFileFormatter();

                var memoryStream = new MemoryStream();
                var writer = new BinaryWriter(memoryStream);

                var downloadLength = 1024;
                var downloadedInfo = new List<DataRange>()
                {
                    new DataRange(0, 10,0),
                    new DataRange(10,20,0),
                    new DataRange(100,2,0)
                };

                var info = new BreakpointResumptionTransmissionInfo(downloadLength, downloadedInfo);
                formatter.Write(writer, info);

                memoryStream.Seek(0, SeekOrigin.Begin);

                var result = await formatter.ReadAsync(memoryStream);

                Assert.IsNotNull(result);

                Assert.AreEqual(downloadLength, result.DownloadLength);
                Assert.IsNotNull(result.DownloadedInfo);
                Assert.AreEqual(downloadedInfo.Count, result.DownloadedInfo.Count);

                for (int i = 0; i < downloadedInfo.Count; i++)
                {
                    Assert.AreEqual(downloadedInfo[i].StartPoint, result.DownloadedInfo[i].StartPoint);
                    Assert.AreEqual(downloadedInfo[i].Length, result.DownloadedInfo[i].Length);
                }
            });
        }
    }
}
