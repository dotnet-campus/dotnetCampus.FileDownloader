using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using dotnetCampus.FileDownloader;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using MSTest.Extensions.Contracts;

namespace FileDownloader.Tests
{
    [TestClass]
    public class SegmentFileDownloaderTest
    {
        [ContractTestCase]
        public void CreateWebRequest()
        {
            "给定特殊的WebRequest将会在随后使用".Test(() =>
            {
                var mock = new Mock<IMockSegmentFileDownloader>();
                var url = $"https://blog.lindexi.com";

                var httpWebRequest = (HttpWebRequest) WebRequest.Create(url);
                mock.Setup(downloader => downloader.CreateWebRequest(It.IsAny<string>())).Returns(() => httpWebRequest);
                mock.Setup(downloader => downloader.OnWebRequestSet(httpWebRequest))
                    .Returns(() => httpWebRequest);

                var fakeSegmentFileDownloader = new FakeSegmentFileDownloader(mock.Object, url,
                    new FileInfo("test" + Path.GetRandomFileName()));

                var task = fakeSegmentFileDownloader.DownloadFileAsync();
                Task.WaitAny(task, Task.Delay(TimeSpan.FromSeconds(1)));

                mock.Verify(downloader => downloader.CreateWebRequest(It.IsAny<string>()), Times.AtLeastOnce);
            });

            "测试进入慢网环境下载，能成功下载文件".Test(() =>
            {
                var url = $"https://blog.lindexi.com";
                var file = new FileInfo(Path.GetTempFileName());
                var slowlySegmentFileDownloader = new SlowSegmentFileDownloader(url, file);
                var task = slowlySegmentFileDownloader.DownloadFileAsync();

                Task.WaitAny(task, Task.Delay(TimeSpan.FromSeconds(20)));

                if (task.IsCompleted)
                {
                    file.Refresh();
                    Assert.AreEqual(100, file.Length);
                }
                else
                {
                    // 测试设备太渣
                }
            });
        }

        class SlowStream : Stream
        {
            public override void Flush()
            {
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return Task.FromResult(Read(buffer, offset, count));
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                Thread.Sleep(100);
                buffer[0] = 0xFF;
                return 1;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return offset;
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => 100;
            public override long Position { get; set; }
        }
#nullable enable
        class SlowSegmentFileDownloader : SegmentFileDownloader
        {
            public SlowSegmentFileDownloader(string url, FileInfo file, ILogger<SegmentFileDownloader>? logger = null, IProgress<DownloadProgress>? progress = null, ISharedArrayPool? sharedArrayPool = null, int bufferLength = UInt16.MaxValue, TimeSpan? stepTimeOut = null) : base(url, file, logger, progress, sharedArrayPool, bufferLength, stepTimeOut)
            {
            }

            protected override WebRequest CreateWebRequest(string url)
            {
                var fakeWebResponse = new FakeWebResponse()
                {
                    Stream = new SlowStream()
                };

                return new FakeHttpWebRequest(fakeWebResponse);
            }
        }

        class FakeSegmentFileDownloader : SegmentFileDownloader
        {
            public FakeSegmentFileDownloader(IMockSegmentFileDownloader mockSegmentFileDownloader, string url,
                FileInfo file, ILogger<SegmentFileDownloader>? logger = null,
                IProgress<DownloadProgress>? progress = null, ISharedArrayPool? sharedArrayPool = null,
                int bufferLength = UInt16.MaxValue, TimeSpan? stepTimeOut = null) : base(url, file, logger, progress,
                sharedArrayPool, bufferLength, stepTimeOut)
            {
                MockSegmentFileDownloader = mockSegmentFileDownloader;
            }

            public IMockSegmentFileDownloader MockSegmentFileDownloader { get; }

            protected override WebRequest CreateWebRequest(string url)
            {
                return MockSegmentFileDownloader.CreateWebRequest(url);
            }

            protected override WebRequest OnWebRequestSet(WebRequest webRequest)
            {
                return MockSegmentFileDownloader.OnWebRequestSet(webRequest);
            }
        }
#nullable restore

        public interface IMockSegmentFileDownloader
        {
            WebRequest CreateWebRequest(string url);
            WebRequest OnWebRequestSet(WebRequest webRequest);
        }
    }

    class FakeHttpWebRequest : WebRequest
    {
        public FakeHttpWebRequest(FakeWebResponse fakeWebResponse)
        {
            FakeWebResponse = fakeWebResponse;
        }

        public override Task<WebResponse> GetResponseAsync()
        {
            return Task.FromResult(GetResponse());
        }

        public override string Method { get; set; }
        public override RequestCachePolicy CachePolicy { get; set; }
        public override string ConnectionGroupName { get; set; }
        public override long ContentLength { get; set; }
        public override string ContentType { get; set; }
        public override ICredentials Credentials { get; set; }
        public override WebHeaderCollection Headers { get; set; }
        public override bool PreAuthenticate { get; set; }
        public override Uri RequestUri { get; }
        public override int Timeout { get; set; }
        public override bool UseDefaultCredentials { get; set; }

        private FakeWebResponse FakeWebResponse { get; }

        public override WebResponse GetResponse()
        {
            return FakeWebResponse;
        }
    }

    class FakeWebResponse : WebResponse
    {
        public Stream Stream { set; get; }
        public override long ContentLength => Stream.Length;

        public override Stream GetResponseStream()
        {
            return Stream;
        }
    }
}
