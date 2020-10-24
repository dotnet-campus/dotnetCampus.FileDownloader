using System;
using System.IO;
using System.Net;
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

                var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
                mock.Setup(downloader => downloader.CreateWebRequest(It.IsAny<string>())).Returns(() => httpWebRequest);
                mock.Setup(downloader => downloader.OnWebRequestSet(httpWebRequest))
                    .Returns(() => httpWebRequest);

                var fakeSegmentFileDownloader = new FakeSegmentFileDownloader(mock.Object, url,
                    new FileInfo("test" + Path.GetRandomFileName()));

                var task = fakeSegmentFileDownloader.DownloadFileAsync();
                Task.WaitAny(task, Task.Delay(TimeSpan.FromSeconds(1)));

                mock.Verify(downloader => downloader.CreateWebRequest(It.IsAny<string>()), Times.AtLeastOnce);
            });
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
}