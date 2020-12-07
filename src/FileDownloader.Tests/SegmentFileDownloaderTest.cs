using System;
using System.Diagnostics;
using System.IO;
using System.Net;
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

            "测试进入弱网环境下载，能成功下载文件".Test(async () =>
            {
                var url = $"https://blog.lindexi.com";
                var file = new FileInfo(Path.GetTempFileName());
                var slowlySegmentFileDownloader = new SlowlySegmentFileDownloader(url, file);
                await slowlySegmentFileDownloader.DownloadFileAsync();
                file.Refresh();
                Assert.AreEqual(100, file.Length);
            });
        }

        class SlowlyStream : Stream
        {
            public override void Flush()
            {
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

            public override bool CanRead { get; }
            public override bool CanSeek { get; }
            public override bool CanWrite { get; }
            public override long Length => 100;
            public override long Position { get; set; }
        }

        class SlowlySegmentFileDownloader : SegmentFileDownloader
        {
            public SlowlySegmentFileDownloader(string url, FileInfo file, ILogger<SegmentFileDownloader>? logger = null, IProgress<DownloadProgress>? progress = null, ISharedArrayPool? sharedArrayPool = null, int bufferLength = UInt16.MaxValue, TimeSpan? stepTimeOut = null) : base(url, file, logger, progress, sharedArrayPool, bufferLength, stepTimeOut)
            {
            }

            protected override HttpWebRequest CreateWebRequest(string url)
            {
                var fakeWebResponse = new FakeWebResponse()
                {
                    Stream = new SlowlyStream()
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

            protected override HttpWebRequest CreateWebRequest(string url)
            {
                return MockSegmentFileDownloader.CreateWebRequest(url);
            }

            protected override HttpWebRequest OnWebRequestSet(HttpWebRequest webRequest)
            {
                return MockSegmentFileDownloader.OnWebRequestSet(webRequest);
            }
        }

        public interface IMockSegmentFileDownloader
        {
            HttpWebRequest CreateWebRequest(string url);
            HttpWebRequest OnWebRequestSet(HttpWebRequest webRequest);
        }
    }

    class FakeHttpWebRequest : HttpWebRequest
    {
        public FakeHttpWebRequest(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext)
        {
        }

        public FakeHttpWebRequest(SerializationInfo serializationInfo, StreamingContext streamingContext, FakeWebResponse fakeWebResponse) : base(serializationInfo, streamingContext)
        {
            FakeWebResponse = fakeWebResponse;
        }

        public FakeHttpWebRequest(FakeWebResponse fakeWebResponse)
        : this(new SerializationInfo(typeof(FakeHttpWebRequest), new FormatterConverter()), new StreamingContext(StreamingContextStates.All), fakeWebResponse)
        {

        }

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
