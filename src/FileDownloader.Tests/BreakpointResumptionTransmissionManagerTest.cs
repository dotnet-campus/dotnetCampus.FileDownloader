#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using dotnetCampus.FileDownloader;
using dotnetCampus.FileDownloader.Utils.BreakpointResumptionTransmissions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using MSTest.Extensions.Contracts;

namespace FileDownloader.Tests
{
    [TestClass]
    public class BreakpointResumptionTransmissionManagerTest
    {
        [ContractTestCase]
        public void GetDownloadSegmentList()
        {
            "传入的断点续传记录的校验信息中，有一半与下载文件不匹配，返回有一半已经下载成功".Test(async () =>
            {
                DebugRange[] dataRanges = new DebugRange[]
                {
                    new(10, 10), // 10-20
                    new(25, 10), // 25-35
                    new(50, 10), // 50-60
                    new(90, 5)   // 90-95
                };

                ISharedArrayPool sharedArrayPool = new SharedArrayPool();
                var fileWriter = new FakeRandomFileWriter();
                var breakpointResumptionTransmissionRecordFile =
                    new System.IO.FileInfo($"BreakpointResumption_{Path.GetRandomFileName()}");
                var manager = new BreakpointResumptionTransmissionManager(breakpointResumptionTransmissionRecordFile,
                    fileWriter, sharedArrayPool, contentLength: DownloadLength);

                var downloadFile = new System.IO.FileInfo($"FooDownloadFile_{Path.GetRandomFileName()}");
                await using var fileStream = downloadFile.Open(FileMode.Create, FileAccess.ReadWrite);

                var segmentManager = await manager.CreateSegmentManagerAsync(fileStream);
                Assert.IsTrue(DownloadLength == segmentManager.FileLength);
                var currentDownloadSegmentList = segmentManager.GetCurrentDownloadSegmentList();
                Assert.AreEqual(0, currentDownloadSegmentList.Count);

                // 通过 FakeRandomFileWriter 写入数据
                var buffer = new byte[DownloadLength];
                Random.Shared.NextBytes(buffer);

                foreach (var (start, length) in dataRanges)
                {
                    fileWriter.QueueWrite(start, buffer, start, length);
                }

                // 关闭文件流，模拟文件写入完成。准备再次新建一个
                manager.Dispose();

                // 对下载文件写入不一样的内容，模拟下载文件和校验内容不匹配
                // 对 1 2 项填充错误的内容
                FillErrorBuffer(dataRanges[1]);
                FillErrorBuffer(dataRanges[2]);
                await fileStream.WriteAsync(buffer, 0, buffer.Length);

                manager = new BreakpointResumptionTransmissionManager(breakpointResumptionTransmissionRecordFile,
                    fileWriter, sharedArrayPool, contentLength: DownloadLength);
                // 再次读取新的列表，此时预期能够读取到具备已下载内容的列表
                var segmentManager2 = await manager.CreateSegmentManagerAsync(fileStream);

                IReadOnlyList<DownloadSegment> downloadSegmentList = segmentManager2.GetCurrentDownloadSegmentList();

                AssertDownloadSegmentList(DownloadLength, downloadSegmentList);

                // 预期读取到一半任何一个已经下载完成的列表内容
                Assert.AreEqual(dataRanges.Length / 2, downloadSegmentList.Count(t => t.LoadingState == DownloadingState.Finished));
                Assert.AreEqual(dataRanges.Length / 2, downloadSegmentList.Count(t => t.Finished));

                AssertDownloadedList(downloadSegmentList, new DebugRange[2]
                {
                    dataRanges[0],
                    dataRanges[3],
                });

                void FillErrorBuffer(DebugRange range)
                {
                    Random.Shared.NextBytes(buffer.AsSpan(range.Start, range.Length));
                }
            });

            "传入的断点续传记录的校验信息全部与下载文件不匹配，返回空列表".Test(async () =>
            {
                DebugRange[] dataRanges = new DebugRange[]
                {
                    new(10, 10),
                    new(20, 10),
                    new(30, 20)
                };

                ISharedArrayPool sharedArrayPool = new SharedArrayPool();
                var fileWriter = new FakeRandomFileWriter();
                var breakpointResumptionTransmissionRecordFile =
                    new System.IO.FileInfo($"BreakpointResumption_{Path.GetRandomFileName()}");
                var manager = new BreakpointResumptionTransmissionManager(breakpointResumptionTransmissionRecordFile,
                    fileWriter, sharedArrayPool, contentLength: DownloadLength);

                var downloadFile = new System.IO.FileInfo($"FooDownloadFile_{Path.GetRandomFileName()}");
                await using var fileStream = downloadFile.Open(FileMode.Create, FileAccess.ReadWrite);

                var segmentManager = await manager.CreateSegmentManagerAsync(fileStream);
                Assert.IsTrue(DownloadLength == segmentManager.FileLength);
                var currentDownloadSegmentList = segmentManager.GetCurrentDownloadSegmentList();
                Assert.AreEqual(0, currentDownloadSegmentList.Count);

                // 通过 FakeRandomFileWriter 写入数据
                var buffer = new byte[DownloadLength];
                Random.Shared.NextBytes(buffer);

                foreach (var (start, length) in dataRanges)
                {
                    fileWriter.QueueWrite(start, buffer, start, length);
                }

                // 关闭文件流，模拟文件写入完成。准备再次新建一个
                manager.Dispose();

                // 对下载文件写入完全不一样的内容，模拟下载文件和校验内容不匹配
                Random.Shared.NextBytes(buffer);
                await fileStream.WriteAsync(buffer, 0, buffer.Length);

                manager = new BreakpointResumptionTransmissionManager(breakpointResumptionTransmissionRecordFile,
                    fileWriter, sharedArrayPool, contentLength: DownloadLength);
                // 再次读取新的列表，此时预期能够读取到具备已下载内容的列表
                var segmentManager2 = await manager.CreateSegmentManagerAsync(fileStream);

                IReadOnlyList<DownloadSegment> downloadSegmentList = segmentManager2.GetCurrentDownloadSegmentList();

                AssertDownloadSegmentList(DownloadLength, downloadSegmentList);

                // 预期读取不到任何一个已经下载完成的列表内容
                Assert.AreEqual(0, downloadSegmentList.Count(t => t.LoadingState == DownloadingState.Finished));
                Assert.AreEqual(0, downloadSegmentList.Count(t => t.Finished));
            });

            "传入下载长度 100 分段分别为 10-20 和 20-30 和 50-55 到 GetDownloadSegmentList 方法，可以获取需要下载的段".Test(async () =>
            {
                ISharedArrayPool sharedArrayPool = new SharedArrayPool();
                var fileWriter = new FakeRandomFileWriter();
                var breakpointResumptionTransmissionRecordFile =
                    new System.IO.FileInfo($"BreakpointResumption_{Path.GetRandomFileName()}");
                var manager = new BreakpointResumptionTransmissionManager(breakpointResumptionTransmissionRecordFile,
                    fileWriter, sharedArrayPool, contentLength: DownloadLength);

                var downloadFile = new System.IO.FileInfo($"FooDownloadFile_{Path.GetRandomFileName()}");
                await using var fileStream = downloadFile.Open(FileMode.Create, FileAccess.ReadWrite);

                var segmentManager = await manager.CreateSegmentManagerAsync(fileStream);
                Assert.IsTrue(DownloadLength == segmentManager.FileLength);
                var currentDownloadSegmentList = segmentManager.GetCurrentDownloadSegmentList();
                Assert.AreEqual(0, currentDownloadSegmentList.Count);

                // 通过 FakeRandomFileWriter 写入数据
                var buffer = new byte[DownloadLength];
                Random.Shared.NextBytes(buffer);
                await fileStream.WriteAsync(buffer, 0, buffer.Length);

                // 10-20
                // 20-30
                // 50-55
                DebugRange[] dataRanges = new DebugRange[]
                {
                    new(10, 10),
                    new(20, 10),
                    new(50, 5)
                };
                foreach (var (start, length) in dataRanges)
                {
                    fileWriter.QueueWrite(start, buffer, start, length);
                }

                // 关闭文件流，模拟文件写入完成。准备再次新建一个
                manager.Dispose();

                manager = new BreakpointResumptionTransmissionManager(breakpointResumptionTransmissionRecordFile,
                    fileWriter, sharedArrayPool, contentLength: DownloadLength);
                // 再次读取新的列表，此时预期能够读取到具备已下载内容的列表
                var segmentManager2 = await manager.CreateSegmentManagerAsync(fileStream);

                IReadOnlyList<DownloadSegment> downloadSegmentList = segmentManager2.GetCurrentDownloadSegmentList();

                AssertDownloadSegmentList(DownloadLength, downloadSegmentList);
                AssertDownloadedList(downloadSegmentList, dataRanges);
            });

            "传入下载长度 100 分段分别为 10-20 和 20-30 和 30-50 到 GetDownloadSegmentList 方法，可以获取需要下载的段".Test(async () =>
            {
                // 10-20
                // 20-30
                // 30-50
                DebugRange[] dataRanges = new DebugRange[]
                {
                    new(10, 10),
                    new(20, 10),
                    new(30, 20)
                };

                ISharedArrayPool sharedArrayPool = new SharedArrayPool();
                var fileWriter = new FakeRandomFileWriter();
                var breakpointResumptionTransmissionRecordFile =
                    new System.IO.FileInfo($"BreakpointResumption_{Path.GetRandomFileName()}");
                var manager = new BreakpointResumptionTransmissionManager(breakpointResumptionTransmissionRecordFile,
                    fileWriter, sharedArrayPool, contentLength: DownloadLength);

                var downloadFile = new System.IO.FileInfo($"FooDownloadFile_{Path.GetRandomFileName()}");
                await using var fileStream = downloadFile.Open(FileMode.Create, FileAccess.ReadWrite);

                var segmentManager = await manager.CreateSegmentManagerAsync(fileStream);
                Assert.IsTrue(DownloadLength == segmentManager.FileLength);
                var currentDownloadSegmentList = segmentManager.GetCurrentDownloadSegmentList();
                Assert.AreEqual(0, currentDownloadSegmentList.Count);

                // 通过 FakeRandomFileWriter 写入数据
                var buffer = new byte[DownloadLength];
                Random.Shared.NextBytes(buffer);
                await fileStream.WriteAsync(buffer, 0, buffer.Length);

                foreach (var (start, length) in dataRanges)
                {
                    fileWriter.QueueWrite(start, buffer, start, length);
                }

                // 关闭文件流，模拟文件写入完成。准备再次新建一个
                manager.Dispose();

                manager = new BreakpointResumptionTransmissionManager(breakpointResumptionTransmissionRecordFile,
                    fileWriter, sharedArrayPool, contentLength: DownloadLength);
                // 再次读取新的列表，此时预期能够读取到具备已下载内容的列表
                var segmentManager2 = await manager.CreateSegmentManagerAsync(fileStream);

                IReadOnlyList<DownloadSegment> downloadSegmentList = segmentManager2.GetCurrentDownloadSegmentList();

                AssertDownloadSegmentList(DownloadLength, downloadSegmentList);
                AssertDownloadedList(downloadSegmentList, dataRanges);
            });

            "传入下载长度 100 分段分别为 10-20 和 30-50 到 GetDownloadSegmentList 方法，可以获取需要下载的段".Test(async () =>
            {
                // 10-20
                // 30-50
                DebugRange[] dataRanges = new DebugRange[]
                {
                    new(10, 20),
                    new(30, 20)
                };

                ISharedArrayPool sharedArrayPool = new SharedArrayPool();
                var fileWriter = new FakeRandomFileWriter();
                var breakpointResumptionTransmissionRecordFile =
                    new System.IO.FileInfo($"BreakpointResumption_{Path.GetRandomFileName()}");
                var manager = new BreakpointResumptionTransmissionManager(breakpointResumptionTransmissionRecordFile,
                    fileWriter, sharedArrayPool, contentLength: DownloadLength);

                var downloadFile = new System.IO.FileInfo($"FooDownloadFile_{Path.GetRandomFileName()}");
                await using var fileStream = downloadFile.Open(FileMode.Create, FileAccess.ReadWrite);

                var segmentManager = await manager.CreateSegmentManagerAsync(fileStream);
                Assert.IsTrue(DownloadLength == segmentManager.FileLength);
                var currentDownloadSegmentList = segmentManager.GetCurrentDownloadSegmentList();
                Assert.AreEqual(0, currentDownloadSegmentList.Count);

                // 通过 FakeRandomFileWriter 写入数据
                var buffer = new byte[DownloadLength];
                Random.Shared.NextBytes(buffer);
                await fileStream.WriteAsync(buffer, 0, buffer.Length);

                foreach (var (start, length) in dataRanges)
                {
                    fileWriter.QueueWrite(start, buffer, start, length);
                }

                // 关闭文件流，模拟文件写入完成。准备再次新建一个
                manager.Dispose();

                manager = new BreakpointResumptionTransmissionManager(breakpointResumptionTransmissionRecordFile,
                    fileWriter, sharedArrayPool, contentLength: DownloadLength);
                // 再次读取新的列表，此时预期能够读取到具备已下载内容的列表
                var segmentManager2 = await manager.CreateSegmentManagerAsync(fileStream);

                IReadOnlyList<DownloadSegment> downloadSegmentList = segmentManager2.GetCurrentDownloadSegmentList();

                AssertDownloadSegmentList(DownloadLength, downloadSegmentList);
                AssertDownloadedList(downloadSegmentList, dataRanges);
            });
        }

        private const int DownloadLength = 100;

        private static void AssertDownloadSegmentList(int downloadLength,
            IReadOnlyList<DownloadSegment> downloadSegmentList)
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

        private void AssertDownloadedList(IReadOnlyList<DownloadSegment> downloadSegmentList, DebugRange[] dataRanges)
        {
            var list = dataRanges.ToList();
            foreach (DownloadSegment downloadSegment in downloadSegmentList)
            {
                if (downloadSegment.LoadingState == DownloadingState.Finished)
                {
                    // 下载完成的，必定是在列表里面
                    DebugRange? range = list.FirstOrDefault(t =>
                        t.Start == downloadSegment.StartPoint && t.Length == downloadSegment.DownloadedLength);
                    Assert.IsNotNull(range);

                    list.Remove(range!);
                }
            }

            // 全部列表记录的，都能从 downloadSegmentList 找到，即被删除
            Assert.AreEqual(0, list.Count);
        }

        record DebugRange(int Start, int Length);

        class FakeRandomFileWriter : IRandomFileWriter
        {
            public void QueueWrite(long fileStartPoint, byte[] data, int dataOffset, int dataLength)
            {
                StepWriteFinished?.Invoke(this,
                    new StepWriteFinishedArgs(fileStartPoint, dataOffset, data, dataLength));
            }

            public event EventHandler<StepWriteFinishedArgs>? StepWriteFinished;

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }
    }
}
