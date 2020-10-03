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
        /// <param name="stepTimeOut">每一步 每一分段下载超时时间 默认 10 秒</param>
        public SegmentFileDownloader(string url, FileInfo file, ILogger<SegmentFileDownloader>? logger = null,
            IProgress<DownloadProgress>? progress = null, ISharedArrayPool? sharedArrayPool = null,
            int bufferLength = ushort.MaxValue, TimeSpan? stepTimeOut = null)
        {
            _logger = logger ?? new DebugSegmentFileDownloaderLogger();
            _progress = progress ?? new Progress<DownloadProgress>();
            SharedArrayPool = sharedArrayPool ?? new SharedArrayPool();
            StepTimeOut = stepTimeOut ?? TimeSpan.FromSeconds(10);

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

        private AsyncQueue<DownloadData> DownloadDataList { get; } = new AsyncQueue<DownloadData>();

        /// <summary>
        /// 下载的文件
        /// </summary>
        public FileInfo File { get; }
        ///// <summary>
        ///// 定时检测的最后时间
        ///// </summary>
        //private DateTime LastTime { get; set; } = DateTime.Now;

        private readonly ILogger<SegmentFileDownloader> _logger;
        private readonly IProgress<DownloadProgress> _progress;

        private bool _isDisposed;

        private IRandomFileWriter FileWriter { set; get; } = null!;

        private FileStream FileStream { set; get; } = null!;

        private TaskCompletionSource<bool> FileDownloadTask { get; } = new TaskCompletionSource<bool>();
        private SegmentManager SegmentManager { set; get; } = null!;
        private int _idGenerator;

        /// <summary>
        /// 每一次分段下载的超时时间，默认10秒
        /// </summary>
        public TimeSpan StepTimeOut { get; }

        //变速器3秒为一周期
        private TimeSpan ControlDelayTime { get; } = TimeSpan.FromSeconds(3);

        /// <summary>
        /// 最大线程数量
        /// </summary>
        private int MaxThreadCount { get; } = 10;

        /// <summary>
        /// 网速控制开关
        /// </summary>
        /// <returns></returns>
        private async void ControlSwitch()
        {
            // 使用独立的线程的优势在于不需要等待下载就能进入方法
            // 可以干掉 LastTime 属性，因此定时是 3 秒
            await Task.Delay(ControlDelayTime);

            while (!SegmentManager.IsFinished())
            {
                _logger.LogDebug("Start ControlSwitch");
                var (segment, runCount, maxReportTime) = SegmentManager.GetDownloadSegmentStatus();
                int waitCount = DownloadDataList.Count;

                _logger.LogDebug("ControlSwitch 当前等待数量：{0},待命最大响应时间：{1},运行数量：{2},运行线程{3}", waitCount, maxReportTime, runCount, _threadCount);

                if (maxReportTime > TimeSpan.FromSeconds(10) && segment != null && runCount > 1)
                {
                    // 此时速度太慢
                    segment.LoadingState = DownloadingState.Pause;
                    _logger.LogDebug("ControlSwitch slowly pause segment={0}", segment.Number);
                }
                else if (maxReportTime < TimeSpan.FromMilliseconds(600) && waitCount > 0 || runCount < 1)
                {
                    // 速度非常快，尝试再开线程，或者当前没有在进行的任务
                    // 如果此时是刚好全部完成了，而 runCount 是 0 进入 StartDownloadTask 也将会啥都不做
                    _logger.LogDebug("ControlSwitch StartDownloadTask");

                    // 这里不需要线程安全，如果刚好全部线程都在退出，等待 ControlDelayTime 再次创建
                    if (_threadCount < MaxThreadCount)
                    {
                        StartDownloadTask();
                    }
                }

                _logger.LogDebug("Finish ControlSwitch");
                //变速器3秒为一周期
                await Task.Delay(ControlDelayTime);
            }
        }

        /// <summary>
        /// 开启线程下载
        /// </summary>
        private void StartDownloadTask()
        {
            _ = Task.Run(DownloadTaskInner);

            async Task DownloadTaskInner()
            {
                Interlocked.Increment(ref _threadCount);
                await DownloadTask();
                Interlocked.Decrement(ref _threadCount);
            }
        }

        private int _threadCount;

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

            int threadCount;

            if (supportSegment)
            {
                // 先根据文件的大小，大概是 1M 让一个线程下载，至少需要开两个线程，最多是 10 个线程
                threadCount = Math.Max(Math.Min(2, (int) (contentLength / 1024 / 1024)), MaxThreadCount);
            }
            else
            {
                // 不支持分段下载下，多个线程也没啥用
                threadCount = 1;
            }

            if (supportSegment)
            {
                // 多创建几个线程下载
                for (var i = 0; i < threadCount; i++)
                {
                    Download(SegmentManager.GetNewDownloadSegment());
                }

                //控制开关，如果下载阻塞就先暂停
                ControlSwitch();
            }

            // 一开始就创建足够量的线程尝试下载
            for (var i = 0; i < threadCount; i++)
            {
                StartDownloadTask();
            }

            await FileDownloadTask.Task;
        }

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
            var id = Interlocked.Increment(ref _idGenerator);

            for (var i = 0; !_isDisposed; i++)
            {
                TimeSpan retryDelayTime = TimeSpan.FromMilliseconds(100);

                try
                {
                    var url = Url;
                    _logger.LogDebug("[GetWebResponseAsync] [{0}] Create WebRequest. Retry Count {0}", id, i);
                    var webRequest = (HttpWebRequest) WebRequest.Create(url);
                    webRequest.Method = "GET";
                    // 加上超时，支持弱网
                    // Timeout设置的是从发出请求开始算起，到与服务器建立连接的时间
                    // ReadWriteTimeout设置的是从建立连接开始，到下载数据完毕所历经的时间
                    // 即使下载速度再慢，只有要在下载，也不能算超时
                    // 如果下载 BufferLength 长度 默认 65535 字节时间超过 10 秒，基本上也断开也差不多
                    webRequest.Timeout = (int) StepTimeOut.TotalMilliseconds;
                    webRequest.ReadWriteTimeout = (int) StepTimeOut.TotalMilliseconds;

                    _logger.LogDebug("[GetWebResponseAsync] [{0}] Enter action.", id);
                    action?.Invoke(webRequest);

                    var stopwatch = Stopwatch.StartNew();
                    _logger.LogDebug("[GetWebResponseAsync] [{0}] Start GetResponseAsync.", id);
                    var response = await webRequest.GetResponseAsync();
                    stopwatch.Stop();
                    _logger.LogDebug("[GetWebResponseAsync] [{0}] Finish GetResponseAsync. Cost time {1} ms", id,
                        stopwatch.ElapsedMilliseconds);

                    return response;
                }
                catch (WebException e)
                {
                    // 如超时或 403 等服务器返回的错误，此时修改重试时间
                    // $exception	{"The operation has timed out."}
                    // $exception	{"The remote server returned an error: (403) Forbidden."}
                    _logger.LogInformation($"[{id}] 第{i}次获取长度失败 {e}");

                    retryDelayTime = TimeSpan.FromSeconds(1);
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
                    _logger.LogInformation($"[{id}] 第{i}次获取长度失败 {e}");
                }

                // 后续需要配置不断下降时间
                _logger.LogDebug("[GetWebResponseAsync] [{0}] Delay {1} ms", id, retryDelayTime.TotalMilliseconds);
                await Task.Delay(retryDelayTime);
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
                // 不需要进行等待，就是开始下载
                //await semaphoreSlim.WaitAsync();
                var data = await DownloadDataList.DequeueAsync();

                // 没有内容了
                if (SegmentManager.IsFinished())
                {
                    return;
                }

                var downloadSegment = data.DownloadSegment;
                downloadSegment.LoadingState = DownloadingState.Runing;

                _logger.LogInformation(
                    $"Download {downloadSegment.StartPoint}-{downloadSegment.CurrentDownloadPoint}/{downloadSegment.RequirementDownloadPoint}");

                downloadSegment.Message = "Start GetWebResponse";
                using var response = data.WebResponse ?? await GetWebResponse(downloadSegment);
                downloadSegment.Message = "Finish GetWebResponse";

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

                if (downloadSegment.Finished)
                {
                    downloadSegment.LoadingState = DownloadingState.Finished;
                    // 下载比较快，尝试再分配一段下载
                    if (downloadSegment.RequirementDownloadPoint - downloadSegment.StartPoint > 1024 * 1024)
                    {
                        // 如果当前下载的内容依然是长段的，也就是 RequirementDownloadPoint-StartPoint 长度比较大，那么下载完成后请求新的下载
                        Download(SegmentManager.GetNewDownloadSegment());
                    }
                }
                else
                {
                    // 如果当前这一段还没完成，那么放回去继续下载，如果是当前下载速度太慢的，暂停一会
                    Download(downloadSegment);

                    if (downloadSegment.LoadingState == DownloadingState.Pause)
                    {
                        // 暂停一会，也就是当前线程退出
                        return;
                    }
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

            downloadSegment.Message = "Start GetResponseStream";
            await using var responseStream = response.GetResponseStream();
            downloadSegment.Message = "Finish GetResponseStream";

            int length = BufferLength;
            Debug.Assert(responseStream != null, nameof(responseStream) + " != null");

            while (!downloadSegment.Finished)
            {
                _logger.LogDebug("[DownloadSegmentInner] Start Rent Array. {0}", downloadSegment);
                var buffer = SharedArrayPool.Rent(length);
                _logger.LogDebug("[DownloadSegmentInner] Finish Rent Array. {0}", downloadSegment);

                downloadSegment.Message = "Start ReadAsync";
                _logger.LogDebug("[DownloadSegmentInner] Start ReadAsync. {0}", downloadSegment);
                using var cancellationTokenSource = new CancellationTokenSource(StepTimeOut);
                // 设置了 WebRequest.Timeout 不能用来修改异步的方法，所以需要使用下面方法
                downloadSegment.LastDownTime = DateTime.Now;
                var n = await responseStream.ReadAsync(buffer, 0, length, cancellationTokenSource.Token);
                _logger.LogDebug("[DownloadSegmentInner] Finish ReadAsync. Length {0} {1}", n, downloadSegment);
                downloadSegment.Message = "Finish ReadAsync";

                if (n <= 0)
                {
                    break;
                }

                LogDownloadSegment(downloadSegment);

                downloadSegment.Message = "QueueWrite";
                _logger.LogDebug("[DownloadSegmentInner] QueueWrite. Start {0} Length {1}",
                    downloadSegment.CurrentDownloadPoint, n);
                FileWriter.QueueWrite(downloadSegment.CurrentDownloadPoint, buffer, 0, n);

                downloadSegment.DownloadedLength += n;

                _progress.Report(new DownloadProgress(SegmentManager));

                if (downloadSegment.LoadingState == DownloadingState.Pause)
                {
                    break;
                }

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