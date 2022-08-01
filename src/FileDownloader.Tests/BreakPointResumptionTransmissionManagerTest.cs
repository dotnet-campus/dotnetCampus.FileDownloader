
using System.Collections.Generic;

using dotnetCampus.FileDownloader;
using dotnetCampus.FileDownloader.Utils.BreakpointResumptionTransmissions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using MSTest.Extensions.Contracts;

namespace FileDownloader.Tests
{
    [TestClass]
    public class BreakPointResumptionTransmissionManagerTest
    {
        [ContractTestCase]
        public void GetDownloadSegmentList()
        {
            "传入下载长度 100 分段分别为 10-20 和 20-30 和 50-55 到 GetDownloadSegmentList 方法，可以获取需要下载的段".Test(() =>
            {
                const int DownloadLength = 100;
                var mock = new Mock<IRandomFileWriter>();
                var manager = new BreakpointResumptionTransmissionManager(new System.IO.FileInfo("Foo"), mock.Object, DownloadLength);

                List<DataRange> list = new List<DataRange>()
                {
                    new DataRange(10,10),// 10-20
                    new DataRange(20,10),// 20-30
                    new DataRange(50,5),// 50-55
                };
                var downloadSegmentList = manager.GetDownloadSegmentList(list);

                AssertDownloadSegmentList(DownloadLength, downloadSegmentList);
            });

            "传入下载长度 100 分段分别为 10-20 和 20-30 和 30-50 到 GetDownloadSegmentList 方法，可以获取需要下载的段".Test(() =>
            {
                const int DownloadLength = 100;
                var mock = new Mock<IRandomFileWriter>();
                var manager = new BreakpointResumptionTransmissionManager(new System.IO.FileInfo("Foo"), mock.Object, DownloadLength);

                List<DataRange> list = new List<DataRange>()
                {
                    new DataRange(10,10),// 10-20
                    new DataRange(20,10),// 20-30
                    new DataRange(30,20),// 30-50
                };
                var downloadSegmentList = manager.GetDownloadSegmentList(list);

                AssertDownloadSegmentList(DownloadLength, downloadSegmentList);
            });

            "传入下载长度 100 分段分别为 10-20 和 30-50 到 GetDownloadSegmentList 方法，可以获取需要下载的段".Test(() =>
            {
                const int DownloadLength = 100;
                var mock = new Mock<IRandomFileWriter>();
                var manager = new BreakpointResumptionTransmissionManager(new System.IO.FileInfo("Foo"), mock.Object, DownloadLength);

                List<DataRange> list = new List<DataRange>()
                {
                    new DataRange(10,10),// 10-20
                    new DataRange(30,20),// 30-50
                };
                var downloadSegmentList = manager.GetDownloadSegmentList(list);

                Assert.IsNotNull(downloadSegmentList);
                Assert.AreEqual(5, downloadSegmentList.Count);
                AssertDownloadSegmentList(DownloadLength, downloadSegmentList);
            });
        }

        private static void AssertDownloadSegmentList(int downloadLength, List<DownloadSegment> downloadSegmentList)
        {
            for (var i = 0; i < downloadSegmentList.Count; i++)
            {
                var item = downloadSegmentList[i];

                // 以下用来测试是否从断点续传生成的数据是正确的
                if (i < downloadSegmentList.Count - 1)
                {
                    var next = downloadSegmentList[i + 1];
                    // 当前的下载到的最后一个点，需要等于下一段的起始。否则将会存在一段没有下载
                    var lastPoint = item.RequirementDownloadPoint;
                    Assert.AreEqual(lastPoint, next.StartPoint);
                }
                else
                {
                    // 最后一段应该下载的点等于下载长度
                    var lastPoint = item.RequirementDownloadPoint;
                    Assert.AreEqual(lastPoint, downloadLength);
                }
            }
        }
    }
}
