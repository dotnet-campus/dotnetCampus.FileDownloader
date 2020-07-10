using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using dotnetCampus.Threading;
using Microsoft.Extensions.Logging;

namespace dotnetCampus.FileDownloader
{
    public class SegmentFileDownloader
    {
        public SegmentFileDownloader(string url, FileInfo file, ILogger<SegmentFileDownloader> logger)
        {
            _logger = logger;
            Url = url;
            File = file;

            logger.BeginScope("Url={url} File={file}", url, file);
        }

        public string Url { get; }

        public FileInfo File { get; }

        /// <summary>
        /// 开始下载文件
        /// </summary>
        /// <returns></returns>
        public async Task DownloadFile()
        {
            _logger.LogInformation($"Start download Url={Url} File={File.FullName}");

            (var response, var contentLength) = await GetContentLength();

            FileStream = File.Create();
            FileStream.SetLength(contentLength);
            FileWriter = new RandomFileWriter(FileStream);

            SegmentManager = new SegmentManager(contentLength);

            var downloadSegment = SegmentManager.GetNewDownloadSegment();

            // 下载第一段
            Download(response, downloadSegment);

            var supportSegment = await TryDownloadLast(contentLength);

            var threadCount = 1;

            if (supportSegment)
            {
                // 多创建几个线程下载
                threadCount = 10;

                for (var i = 0; i < threadCount; i++)
                {
                    Download(SegmentManager.GetNewDownloadSegment());
                }
            }

            for (var i = 0; i < threadCount; i++)
            {
                _ = Task.Run(DownloadTask);
            }

            await FileDownloadTask.Task;
        }

        private readonly ILogger<SegmentFileDownloader> _logger;

        private bool _isDisposing;

        private RandomFileWriter FileWriter { set; get; }

        private FileStream FileStream { set; get; }

        private TaskCompletionSource<bool> FileDownloadTask { get; } = new TaskCompletionSource<bool>();

        private SegmentManager SegmentManager { set; get; }

        /// <summary>
        /// 获取整个下载的长度
        /// </summary>
        /// <returns></returns>
        private async Task<(WebResponse response, long contentLength)> GetContentLength()
        {
            _logger.LogInformation("开始获取整个下载长度");

            var response = await GetWebResponseAsync();

            if (response == null)
            {
                return default;
            }

            var contentLength = response.ContentLength;

            _logger.LogInformation(
                $"完成获取文件长度，文件长度 {contentLength} {contentLength / 1024}KB {contentLength / 1024.0 / 1024.0:0.00}MB");

            return (response, contentLength);
        }

        private async Task<WebResponse> GetWebResponseAsync(Action<HttpWebRequest> action = null)
        {
            for (var i = 0; !_isDisposing; i++)
            {
                try
                {
                    var url = Url;
                    var webRequest = (HttpWebRequest)WebRequest.Create(url);
                    webRequest.Method = "GET";

                    action?.Invoke(webRequest);

                    var response = await webRequest.GetResponseAsync();

                    return response;
                }
                catch (Exception e)
                {
                    _logger.LogInformation($"第{i}次获取长度失败 {e}");
                }

                // 后续需要配置不断下降时间
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }

            return null;
        }

        /// <summary>
        /// 尝试获取链接响应
        /// </summary>
        /// <param name="downloadSegment"></param>
        /// <returns></returns>
        private async Task<WebResponse> GetWebResponse(DownloadSegment downloadSegment)
        {
            _logger.LogInformation(
                $"Start Get WebResponse{downloadSegment.StartPoint}-{downloadSegment.CurrentDownloadPoint}/{downloadSegment.RequirementDownloadPoint}");

            // 为什么不使用 StartPoint 而是使用 CurrentDownloadPoint 是因为需要处理重试

            var response = await GetWebResponseAsync(webRequest => webRequest.AddRange(downloadSegment.CurrentDownloadPoint, downloadSegment.RequirementDownloadPoint));
            return response;
        }

        private async Task DownloadTask()
        {
            while (!SegmentManager.IsFinished())
            {
                var data = await DownloadDataList.DequeueAsync();

                var downloadSegment = data.DownloadSegment;

                _logger.LogInformation(
                    $"Download {downloadSegment.StartPoint}-{downloadSegment.CurrentDownloadPoint}/{downloadSegment.RequirementDownloadPoint}");

                using var response = data.WebResponse ?? await GetWebResponse(downloadSegment);

                try
                {
                    await using var responseStream = response.GetResponseStream();
                    const int length = 1024;
                    Debug.Assert(responseStream != null, nameof(responseStream) + " != null");

                    while (!downloadSegment.Finished)
                    {
                        var buffer = SharedArrayPool.Rent(length);
                        var n = await responseStream.ReadAsync(buffer, 0, length);

                        if (n < 0)
                        {
                            break;
                        }

                        _logger.LogInformation(
                            $"Download  {downloadSegment.CurrentDownloadPoint * 100.0 / downloadSegment.RequirementDownloadPoint:0.00} Thread {Thread.CurrentThread.ManagedThreadId} {downloadSegment.StartPoint}-{downloadSegment.CurrentDownloadPoint}/{downloadSegment.RequirementDownloadPoint}");

                        FileWriter.WriteAsync(downloadSegment.CurrentDownloadPoint, buffer, n);

                        downloadSegment.DownloadedLength += n;

                        if (downloadSegment.Finished)
                        {
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogInformation(
                        $"Download {downloadSegment.StartPoint}-{downloadSegment.RequirementDownloadPoint} error {e}");

                    // 下载失败了，那么放回去继续下载
                    Download(downloadSegment);
                }

                // 下载比较快，尝试再分配一段下载
                if (downloadSegment.RequirementDownloadPoint - downloadSegment.StartPoint > 1024 * 1024)
                {
                    Download(SegmentManager.GetNewDownloadSegment());
                }
            }

            await FinishDownload();
        }

        private void Download(WebResponse webResponse, DownloadSegment downloadSegment)
        {
            DownloadDataList.Enqueue(new DownloadData(webResponse, downloadSegment));
        }

        private void Download(DownloadSegment downloadSegment)
        {
            Download(null, downloadSegment);
        }

        private AsyncQueue<DownloadData> DownloadDataList { get; } = new AsyncQueue<DownloadData>();

        private async Task FinishDownload()
        {
            if (_isDisposing)
            {
                return;
            }

            lock (FileDownloadTask)
            {
                if (_isDisposing)
                {
                    return;
                }

                _isDisposing = true;
            }

            await FileWriter.DisposeAsync();
            await FileStream.DisposeAsync();

            FileDownloadTask.SetResult(true);
        }

        private async Task<bool> TryDownloadLast(long contentLength)
        {
            // 尝试下载后部分，如果可以下载后续的 100 个字节，那么这个链接支持分段下载
            const int downloadLength = 100;

            var startPoint = contentLength - downloadLength;

            var responseLast = await GetWebResponseAsync(webRequest =>
            {
                webRequest.AddRange(startPoint, contentLength);
            });

            if (responseLast.ContentLength == downloadLength)
            {
                var downloadSegment = new DownloadSegment(startPoint, contentLength);
                SegmentManager.RegisterDownloadSegment(downloadSegment);

                Download(responseLast, downloadSegment);

                return true;
            }

            return false;
        }

        private class DownloadData
        {
            public DownloadData(WebResponse webResponse, DownloadSegment downloadSegment)
            {
                WebResponse = webResponse;
                DownloadSegment = downloadSegment;
            }

            public WebResponse WebResponse { get; }

            public DownloadSegment DownloadSegment { get; }
        }
    }
}