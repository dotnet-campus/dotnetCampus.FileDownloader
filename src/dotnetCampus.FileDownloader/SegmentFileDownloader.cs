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
    /// <summary>
    /// 分段文件下载器
    /// </summary>
    public class SegmentFileDownloader
    {
        /// <summary>
        /// 创建分段文件下载器
        /// </summary>
        /// <param name="url">下载链接，不对下载链接是否有效进行校对</param>
        /// <param name="file">下载的文件路径</param>
        /// <param name="logger">下载时的内部日志，默认将使用 Debug 输出</param>
        /// <param name="progress">下载进度</param>
        /// <param name="sharedArrayPool">共享缓存数组池，默认使用 ArrayPool 池</param>
        /// <param name="bufferLength">缓存的数组长度，默认是 65535 的长度</param>
        public SegmentFileDownloader(string url, FileInfo file, ILogger<SegmentFileDownloader>? logger = null,
            IProgress<DownloadProgress>? progress = null, ISharedArrayPool? sharedArrayPool = null,
            int bufferLength = ushort.MaxValue)
        {
            _logger = logger ?? new DebugSegmentFileDownloaderLogger();
            _progress = progress ?? new Progress<DownloadProgress>();
            SharedArrayPool = sharedArrayPool ?? new SharedArrayPool();

            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentNullException(nameof(url));
            }

            Url = url;
            File = file ?? throw new ArgumentNullException(nameof(file));

            _logger.BeginScope("Url={url} File={file}", url, file);

            BufferLength = bufferLength;
        }

        /// <summary>
        /// 缓存的数组长度，默认是 65535 的长度
        /// </summary>
        public int BufferLength { get; }

        /// <summary>
        /// 共享缓存数组池，默认使用 ArrayPool 池
        /// </summary>
        public ISharedArrayPool SharedArrayPool { get; }

        /// <summary>
        /// 下载链接
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// 下载的文件
        /// </summary>
        public FileInfo File { get; }

        /// <summary>
        /// 开始下载文件
        /// </summary>
        /// <returns></returns>
        public async Task DownloadFileAsync()
        {
            _logger.LogInformation($"Start download Url={Url} File={File.FullName}");

            var (response, contentLength) = await GetContentLength();

            _logger.LogInformation($"ContentLength={contentLength}");

            if (contentLength < 0)
            {
                // contentLength == -1
                // 当前非下载内容，没有存在长度
                // 可测试使用的链接是 https://dotnet.microsoft.com/download/dotnet/thank-you/sdk-5.0.100-preview.7-windows-x64-installer
                _logger.LogWarning($"Can not download file. ContentLength={contentLength}");
                return;
            }

            FileStream = File.Create();
            FileStream.SetLength(contentLength);
            FileWriter = new RandomFileWriterWithOrderFirst(FileStream);
            FileWriter.StepWriteFinished += (sender, args) => SharedArrayPool.Return(args.Data);

            SegmentManager = new SegmentManager(contentLength);

            _progress.Report(new DownloadProgress($"file length = {contentLength}", SegmentManager));

            var downloadSegment = SegmentManager.GetNewDownloadSegment();

            // 下载第一段
            Download(response, downloadSegment!);

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
        private readonly IProgress<DownloadProgress> _progress;

        private bool _isDisposed;

        private IRandomFileWriter FileWriter { set; get; } = null!;

        private FileStream FileStream { set; get; } = null!;

        private TaskCompletionSource<bool> FileDownloadTask { get; } = new TaskCompletionSource<bool>();

        private SegmentManager SegmentManager { set; get; } = null!;

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

        private async Task<WebResponse?> GetWebResponseAsync(Action<HttpWebRequest>? action = null)
        {
            for (var i = 0; !_isDisposed; i++)
            {
                try
                {
                    var url = Url;
                    _logger.LogDebug("[GetWebResponseAsync] Create WebRequest. Retry Count {0}", i);
                    var webRequest = (HttpWebRequest)WebRequest.Create(url);
                    webRequest.Method = "GET";

                    _logger.LogDebug("[GetWebResponseAsync] Enter action.");
                    action?.Invoke(webRequest);

                    var stopwatch = Stopwatch.StartNew();
                    _logger.LogDebug("[GetWebResponseAsync] Start GetResponseAsync.");
                    var response = await webRequest.GetResponseAsync();
                    stopwatch.Stop();
                    _logger.LogDebug("[GetWebResponseAsync] Finish GetResponseAsync. Cost time {0} ms", stopwatch.ElapsedMilliseconds);

                    return response;
                }
                catch (InvalidCastException)
                {
                    throw;
                }
                catch (NotSupportedException)
                {
                    throw;
                }
                catch (ArgumentException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    // +		$exception	{"The operation has timed out."}	System.Net.WebException
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
        private async Task<WebResponse?> GetWebResponse(DownloadSegment downloadSegment)
        {
            _logger.LogInformation(
                $"Start Get WebResponse{downloadSegment.StartPoint}-{downloadSegment.CurrentDownloadPoint}/{downloadSegment.RequirementDownloadPoint}");

            // 为什么不使用 StartPoint 而是使用 CurrentDownloadPoint 是因为需要处理重试

            var response = await GetWebResponseAsync(webRequest =>
                webRequest.AddRange(downloadSegment.CurrentDownloadPoint, downloadSegment.RequirementDownloadPoint));
            return response;
        }

        private async Task DownloadTask()
        {
            while (!SegmentManager.IsFinished())
            {
                var data = await DownloadDataList.DequeueAsync();

                // 没有内容了
                if (SegmentManager.IsFinished())
                {
                    return;
                }

                var downloadSegment = data.DownloadSegment;

                _logger.LogInformation(
                    $"Download {downloadSegment.StartPoint}-{downloadSegment.CurrentDownloadPoint}/{downloadSegment.RequirementDownloadPoint}");

                using var response = data.WebResponse ?? await GetWebResponse(downloadSegment);

                try
                {
                    await DownloadSegmentInner(response, downloadSegment);
                }
                catch (Exception e)
                {
                    // error System.IO.IOException:  Received an unexpected EOF or 0 bytes from the transport stream.

                    _logger.LogInformation(
                        $"Download {downloadSegment.StartPoint}-{downloadSegment.RequirementDownloadPoint} error {e}");
                    // 在下面代码放回去继续下载
                }

                // 下载比较快，尝试再分配一段下载
                if (downloadSegment.RequirementDownloadPoint - downloadSegment.StartPoint > 1024 * 1024)
                {
                    // 如果当前下载的内容依然是长段的，也就是 RequirementDownloadPoint-StartPoint 长度比较大，那么下载完成后请求新的下载
                    Download(SegmentManager.GetNewDownloadSegment());
                }

                // 如果当前这一段还没完成，那么放回去继续下载
                if (!downloadSegment.Finished)
                {
                    Download(downloadSegment);
                }
            }

            await FinishDownload();
        }

        /// <summary>
        /// 下载的主要逻辑
        /// </summary>
        /// <param name="response"></param>
        /// <param name="downloadSegment"></param>
        /// <returns></returns>
        /// 这个方法如果触发异常，将会在上一层进行重试
        private async Task DownloadSegmentInner(WebResponse? response, DownloadSegment downloadSegment)
        {
            if (response == null)
            {
                // 继续下一次
                throw new WebResponseException("Can not response");
            }

            await using var responseStream = response.GetResponseStream();
            int length = BufferLength;
            Debug.Assert(responseStream != null, nameof(responseStream) + " != null");

            while (!downloadSegment.Finished)
            {
                _logger.LogDebug("[DownloadSegmentInner] Start Rent Array. {0}", downloadSegment);
                var buffer = SharedArrayPool.Rent(length);
                _logger.LogDebug("[DownloadSegmentInner] Finish Rent Array. {0}", downloadSegment);

                _logger.LogDebug("[DownloadSegmentInner] Start ReadAsync. {0}", downloadSegment);
                var n = await responseStream.ReadAsync(buffer, 0, length);
                _logger.LogDebug("[DownloadSegmentInner] Finish ReadAsync. Length {0} {1}", n, downloadSegment);

                if (n <= 0)
                {
                    break;
                }

                LogDownloadSegment(downloadSegment);

                _logger.LogDebug("[DownloadSegmentInner] QueueWrite. Start {0} Length {1}",
                    downloadSegment.CurrentDownloadPoint, n);
                FileWriter.QueueWrite(downloadSegment.CurrentDownloadPoint, buffer, 0, n);

                downloadSegment.DownloadedLength += n;

                _progress.Report(new DownloadProgress(SegmentManager));

                if (downloadSegment.Finished)
                {
                    break;
                }
            }
        }

        private void LogDownloadSegment(DownloadSegment downloadSegment)
        {
            _logger.LogInformation(
                $"Download  {downloadSegment.CurrentDownloadPoint * 100.0 / downloadSegment.RequirementDownloadPoint:0.00} Thread {Thread.CurrentThread.ManagedThreadId} {downloadSegment.StartPoint}-{downloadSegment.CurrentDownloadPoint}/{downloadSegment.RequirementDownloadPoint}");
        }

        private void Download(WebResponse? webResponse, DownloadSegment downloadSegment)
        {
            _logger.LogDebug("[Download] Enqueue Download. {0}", downloadSegment);
            DownloadDataList.Enqueue(new DownloadData(webResponse, downloadSegment));
        }

        private void Download(DownloadSegment? downloadSegment)
        {
            if (downloadSegment == null)
            {
                return;
            }

            Download(null, downloadSegment);
        }

        private AsyncQueue<DownloadData> DownloadDataList { get; } = new AsyncQueue<DownloadData>();

        private async Task FinishDownload()
        {
            if (_isDisposed)
            {
                return;
            }

            lock (FileDownloadTask)
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
            }

            await FileWriter.DisposeAsync();
            await FileStream.DisposeAsync();
            DownloadDataList.Dispose();

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

            if (responseLast == null)
            {
                return false;
            }

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
            public DownloadData(WebResponse? webResponse, DownloadSegment downloadSegment)
            {
                WebResponse = webResponse;
                DownloadSegment = downloadSegment;
            }

            public WebResponse? WebResponse { get; }

            public DownloadSegment DownloadSegment { get; }
        }
    }
}