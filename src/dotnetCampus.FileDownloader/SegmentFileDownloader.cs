using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using dotnetCampus.FileDownloader.Utils;
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

        /// <summary>
        /// 最大线程数量
        /// </summary>
        private int MaxThreadCount { get; } = 10;

        #region 网速控制开关

        /// <summary>
        /// 变速器
        /// </summary>
        /// 默认3秒为一周期，每次将会根据当前网络响应时间决定下载线程数量。网络响应时间很慢，减少线程数量，提升性能
        private TimeSpan ControlDelayTime { get; } = TimeSpan.FromSeconds(3);

        /// <summary>
        /// 表示网络响应速度慢的时间，超过此时间表示当前网络响应速度慢
        /// <para>默认是 10 秒，如果等待 10 秒都没有下载到任何内容，那证明这个网络响应速度慢，可以减少一些线程</para>
        /// <para>这是经验值</para>
        /// </summary>
        private TimeSpan MinSlowlyResponseTime { get; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 表示网络响应速度快的时间，小于此时间表示当前网络响应速度快
        /// <para>默认是 600ms 如果每次都在此时间内能下载到内容，那么加上一些线程，让下载速度更快。依然受到 <see cref="MaxThreadCount"/> 的限制</para>
        /// <para>这是经验值</para>
        /// </summary>
        private TimeSpan MaxFastResponseTime { get; } = TimeSpan.FromMilliseconds(600);

        /// <summary>
        /// 网速控制开关，作用是在弱网下减少线程数量，等待网络速度恢复时，才加大线程数量，用于提升性能
        /// </summary>
        /// 在网络很弱时，网络的响应很慢时，开启多个线程只会空白运行，不会加快任何速度，此时将会逐步降低为采用 1 个线程进行下载
        /// 
        /// 核心逻辑是通过网络的响应时间距离当前时间的距离判断，拿出距离最大的任务，如果时间距离超过了 10 秒，设置任务为暂停，释放此线程
        /// 感谢 [@maplewei](https://github.com/maplewei) 提供的方法
        /// <returns></returns>
        private async void ControlSwitch()
        {
            // 使用独立的线程的优势在于不需要等待下载就能进入方法
            // 可以干掉 LastTime 属性，因此定时是 3 秒
            await Task.Delay(ControlDelayTime);

            while (!SegmentManager.IsFinished())
            {
                LogDebugInternal("Start ControlSwitch");
                var (maxWaitReportTimeDownloadSegment, runCount, maxReportTime) = SegmentManager.GetMaxWaitReportTimeDownloadSegmentStatus();
                int waitCount = DownloadDataList.Count;

                LogDebugInternal("ControlSwitch 当前等待数量：{0},待命最大响应时间：{1},运行数量：{2},运行线程{3}", waitCount, maxReportTime, runCount, _threadCount);

                if (maxReportTime > MinSlowlyResponseTime && maxWaitReportTimeDownloadSegment != null && runCount > 1)
                {
                    // 此时速度太慢，运行的线程也超过一个，那么将太长时间没有响应的任务暂停
                    // 这样可以减少下载器占用的线程数量，网络响应慢不是网络下载速度慢，如果下载速度慢，那依然是有响应的，此时多个线程也拿不到响应，不如减少线程数量，提升性能
                    maxWaitReportTimeDownloadSegment.LoadingState = DownloadingState.Pause;
                    LogDebugInternal("ControlSwitch slowly pause segment={0}", maxWaitReportTimeDownloadSegment.Number);
                }
                else
                {
                    if (maxReportTime < MaxFastResponseTime && waitCount > 0 || runCount < 1)
                    {
                        // 速度非常快，尝试再开线程，或者当前没有在进行的任务
                        // 如果此时是刚好全部完成了，而 runCount 是 0 进入 StartDownloadTask 也将会啥都不做
                        LogDebugInternal("ControlSwitch StartDownloadTask");

                        // 这里不需要线程安全，如果刚好全部线程都在退出，等待 ControlDelayTime 再次创建
                        if (_threadCount < MaxThreadCount)
                        {
                            StartDownloadTask();
                        }
                    }
                    // 如果全部线程都在退出完成，那么重新创建线程
                    else if (_threadCount == 0)
                    {
                        // 网络很弱，响应速度很慢，此时逐步减少线程的过程，刚好遇到线程退出。此时也许有任务依然状态是在执行，但是没有线程去执行这个任务。解决方法就是暂停掉对应的任务，然后重新开启线程，在开启的线程决定如何启动任务
                        // 这是多线程占用的坑，为了减少同步，因此放在这重新设置值
                        if(maxWaitReportTimeDownloadSegment?.LoadingState == DownloadingState.Runing)
                        {
                            maxWaitReportTimeDownloadSegment.LoadingState = DownloadingState.Pause;
                        }
                        StartDownloadTask();
                    }
                }

                LogDebugInternal("Finish ControlSwitch");
                //变速器3秒为一周期
                await Task.Delay(ControlDelayTime);
            }
        }
        #endregion

        /// <summary>
        /// 开启线程下载
        /// </summary>
        private void StartDownloadTask()
        {
            _ = Task.Run(DownloadTaskInner);

            async Task DownloadTaskInner()
            {
                Interlocked.Increment(ref _threadCount);
                try
                {
                    await DownloadTask();
                }
                finally
                {
                    Interlocked.Decrement(ref _threadCount);
                }
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

            if (contentLength <= 0)
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
                threadCount = Math.Min(MaxThreadCount, Math.Max(2, (int) (contentLength / 1024 / 1024)));
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

        /// <summary>
        /// 通过 Url 创建出对应的 <see cref="WebRequest"/> 实例
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        protected virtual WebRequest CreateWebRequest(string url) => (WebRequest) WebRequest.Create(url);

        /// <summary>
        /// 在 <see cref="WebRequest"/> 经过了应用设置之后调用，应用的设置包括下载的 Range 等值，调用这个方法之后的下一步将会是使用这个方法的返回值去下载文件
        /// </summary>
        /// <param name="webRequest"></param>
        /// <returns></returns>
        protected virtual WebRequest OnWebRequestSet(WebRequest webRequest) => webRequest;

        /// <summary>
        /// 这是给我自己开发调试用的
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        [Conditional("DEBUG")]
        private void LogDebugInternal(string message, params object[] args)
        {
            _logger.LogDebug(message, args);
            Debug.WriteLine(message, args);
        }

        private async Task<WebResponse?> GetWebResponseAsync(Action<WebRequest>? action = null)
        {
            var id = Interlocked.Increment(ref _idGenerator);

            for (var i = 0; !_isDisposed; i++)
            {
                TimeSpan retryDelayTime = TimeSpan.FromMilliseconds(100);

                try
                {
                    var url = Url;
                    LogDebugInternal("[GetWebResponseAsync] [{0}] Create WebRequest. Retry Count {0}", id, i);
                    var webRequest = CreateWebRequest(url);
                    webRequest.Method = "GET";
                    // 加上超时，支持弱网
                    // Timeout设置的是从发出请求开始算起，到与服务器建立连接的时间
                    // ReadWriteTimeout设置的是从建立连接开始，到下载数据完毕所历经的时间
                    // 即使下载速度再慢，只有要在下载，也不能算超时
                    // 如果下载 BufferLength 长度 默认 65535 字节时间超过 10 秒，基本上也断开也差不多
                    webRequest.Timeout = (int) StepTimeOut.TotalMilliseconds;

                    if (webRequest is HttpWebRequest httpWebRequest)
                    {
                        // ReadWriteTimeout设置的是从建立连接开始，到下载数据完毕所历经的时间
                        httpWebRequest.ReadWriteTimeout = (int) StepTimeOut.TotalMilliseconds;
                    }

                    LogDebugInternal("[GetWebResponseAsync] [{0}] Enter action.", id);
                    action?.Invoke(webRequest);
                    webRequest = OnWebRequestSet(webRequest);

                    var stopwatch = Stopwatch.StartNew();
                    LogDebugInternal("[GetWebResponseAsync] [{0}] Start GetResponseAsync.", id);
                    var response = await GetResponseAsync(webRequest);
                    stopwatch.Stop();
                    LogDebugInternal("[GetWebResponseAsync] [{0}] Finish GetResponseAsync. Cost time {1} ms", id,
                        stopwatch.ElapsedMilliseconds);

                    return response;
                }
                catch (WebException e)
                {
                    // 如超时或 403 等服务器返回的错误，此时修改重试时间
                    // $exception	{"The operation has timed out."}
                    // $exception	{"The remote server returned an error: (403) Forbidden."}
                    _logger.LogInformation($"[{id}] 第{i}次获取 WebResponse 失败 {e}");

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
                LogDebugInternal("[GetWebResponseAsync] [{0}] Delay {1} ms", id, retryDelayTime.TotalMilliseconds);
                await Task.Delay(retryDelayTime);
            }

            return null;
        }

        /// <summary>
        /// 给继承的类可以从 <paramref name="request"/> 获取消息
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        protected virtual Task<WebResponse> GetResponseAsync(WebRequest request)
            => request.GetResponseAsync();

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
            {
                webRequest.AddRange(downloadSegment.CurrentDownloadPoint, downloadSegment.RequirementDownloadPoint);
            });
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
                    // TaskCanceledException 读取超时，网络速度不够
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
#if NETCOREAPP
            await
#endif
            using var responseStream = response.GetResponseStream();
            downloadSegment.Message = "Finish GetResponseStream";

            int length = BufferLength;
            Debug.Assert(responseStream != null, nameof(responseStream) + " != null");

            while (!downloadSegment.Finished)
            {
                LogDebugInternal("[DownloadSegmentInner] Start Rent Array. {0}", downloadSegment);
                var buffer = SharedArrayPool.Rent(length);
                LogDebugInternal("[DownloadSegmentInner] Finish Rent Array. {0}", downloadSegment);

                downloadSegment.Message = "Start ReadAsync";
                LogDebugInternal("[DownloadSegmentInner] Start ReadAsync. {0}", downloadSegment);
                using var cancellationTokenSource = new CancellationTokenSource(StepTimeOut);
                // 设置了 WebRequest.Timeout 不能用来修改异步的方法，所以需要使用下面方法
                downloadSegment.LastDownTime = DateTime.Now;
                var n = await responseStream.ReadAsync(buffer, 0, length, cancellationTokenSource.Token);
                LogDebugInternal("[DownloadSegmentInner] Finish ReadAsync. Length {0} {1}", n, downloadSegment);
                downloadSegment.Message = "Finish ReadAsync";

                if (n <= 0)
                {
                    break;
                }

                LogDownloadSegment(downloadSegment);

                downloadSegment.Message = "QueueWrite";
                LogDebugInternal("[DownloadSegmentInner] QueueWrite. Start {0} Length {1}",
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
            LogDebugInternal("[Download] Enqueue Download. {0}", downloadSegment);
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
#if NETCOREAPP
            await FileStream.DisposeAsync();
#else
            FileStream.Dispose();
#endif
            await DownloadDataList.DisposeAsync();

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
