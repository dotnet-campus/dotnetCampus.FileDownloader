using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using dotnetCampus.FileDownloader.Utils.BreakpointResumptionTransmissions;
using dotnetCampus.Threading;

using Microsoft.Extensions.Logging;

namespace dotnetCampus.FileDownloader;

/// <summary>
/// 底层通过 HttpClient 进行网络通讯的分段文件下载器
/// </summary>
public class SegmentFileDownloaderByHttpClient : IDisposable
{
    /// <summary>
    /// 创建分段文件下载器
    /// </summary>
    /// <param name="url">下载链接，不对下载链接是否有效进行校对</param>
    /// <param name="file">下载的文件路径</param>
    /// <param name="httpClient">用于通讯的 HttpClient 对象。传入的对象将不会在 <see cref="SegmentFileDownloaderByHttpClient"/> 里被释放。如不传入，则在 <see cref="SegmentFileDownloaderByHttpClient"/> 里自动创建和释放</param>
    /// <param name="logger">下载时的内部日志，默认将使用 Debug 输出</param>
    /// <param name="progress">下载进度</param>
    /// <param name="sharedArrayPool">共享缓存数组池，默认使用 ArrayPool 池</param>
    /// <param name="bufferLength">缓存的数组长度，默认是 65535 的长度</param>
    /// <param name="stepTimeOut">每一步 每一分段下载超时时间 默认 10 秒</param>
    /// <param name="breakpointResumptionTransmissionRecordFile">断点续下的信息记录文件，如为空将不带上断点续下功能。下载完成，自动删除断点续传记录文件</param>
    public SegmentFileDownloaderByHttpClient(string url, FileInfo file,
        HttpClient? httpClient = null,
        ILogger<SegmentFileDownloader>? logger = null,
            IProgress<DownloadProgress>? progress = null, ISharedArrayPool? sharedArrayPool = null,
            int bufferLength = ushort.MaxValue, TimeSpan? stepTimeOut = null, FileInfo? breakpointResumptionTransmissionRecordFile = null)
    {
        _logger = logger ?? new DebugSegmentFileDownloaderLogger();
        _progress = progress ?? new Progress<DownloadProgress>();
        SharedArrayPool = sharedArrayPool ?? new SharedArrayPool();
        StepTimeOut = stepTimeOut ?? TimeSpan.FromSeconds(10);
        BreakpointResumptionTransmissionRecordFile = breakpointResumptionTransmissionRecordFile;
        if (string.IsNullOrEmpty(url))
        {
            throw new ArgumentNullException(nameof(url));
        }

        Url = url;
        File = file ?? throw new ArgumentNullException(nameof(file));

        _logger.BeginScope("Url={url} File={file}", url, file);

        BufferLength = bufferLength;

        _shouldDisposeHttpClient = httpClient is null;
        HttpClient = httpClient ?? CreateDefaultHttpClient();

        DownloadDataList = Channel.CreateUnbounded<DownloadData>(new UnboundedChannelOptions()
        {
            SingleReader = false,
            AllowSynchronousContinuations = true,
        });
    }

    #region HttpClient

    private HttpClient HttpClient { get; }

    /// <summary>
    /// 是否需要释放 HttpClient 对象。如果是外部传入的，那就不需要释放，交给外部去进行释放
    /// </summary>
    private readonly bool _shouldDisposeHttpClient;

    private static HttpClient CreateDefaultHttpClient()
    {
        var socketsHttpHandler = CreateDefaultSocketsHttpHandler();
        return new HttpClient(socketsHttpHandler);
    }

    private static SocketsHttpHandler CreateDefaultSocketsHttpHandler()
    {
        var socketsHttpHandler = new SocketsHttpHandler()
        {
            // 设置超时时间 30 秒，时间和 .NET Framework 版本保持相同
            ConnectTimeout = TimeSpan.FromSeconds(30),
            // 连接池的空闲时间，默认值就是一分钟，如果一分钟没有重复连接，那就释放此连接
            //PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
            // 设置距离多长时间内创建的连接是可以被复用的，设置为半个钟，超过半个钟需要废弃，重新请求 DNS 等，默认值是无穷。这将会在 DNS 更新时，依然访问之前的地址
            PooledConnectionLifetime = TimeSpan.FromMinutes(30),
#if NET6_0_OR_GREATER
            // 开启多路复用，预计能减少后台或CDN压力
            EnableMultipleHttp2Connections = true,
#endif

            // 允许重定向，默认是允许
            //AllowAutoRedirect = true,
            // 最大重定向次数，默认是 50 次
            //MaxAutomaticRedirections = 50,

            // ConnectCallback允许自定义创建新连接。每次打开一个新的TCP连接时都会调用它。回调可用于建立进程内传输、控制DNS解析、控制基础套接字的通用或特定于平台的选项，或者仅用于在新连接打开时通知
            // 每次连接进来时的设置，可以用来动态修改连接的方式，甚至用此方式实现域名备份。当然，现在没有使用此方式做域名备份
            //ConnectCallback = async (context, token) =>
            //{
            // 回调有以下注意事项:
            // - 传递给它的是确定远程端点的DnsEndPoint和发起创建连接的HttpRequestMessage。
            // - 由于SocketsHttpHandler提供了连接池，所创建的连接可以用于处理多个后续请求，而不仅仅是初始请求。将返回一个新的流。
            // - 回调不应该尝试建立TLS会话。这是随后由SocketsHttpHandler处理的。

            //    Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            //    // Turn off Nagle's algorithm since it degrades performance in most HttpClient scenarios.
            //    socket.NoDelay = true;
            //    try
            //    {
            //        await socket.ConnectAsync(context.DnsEndPoint, token).ConfigureAwait(false);
            //        // The stream should take the ownership of the underlying socket,
            //        // closing it when it's disposed.
            //        return new NetworkStream(socket, ownsSocket: true);
            //    }
            //    catch
            //    {
            //        socket.Dispose();
            //        throw;
            //    }
            //},

            // PlaintextStreamFilter 允许在新打开的连接上插入一个自定义层。在连接完全建立之后(包括用于安全连接的TLS握手)，但在发送任何HTTP请求之前调用此回调。因此，可以使用它来监听通过安全连接发送的纯文本数据
            // 过滤所有的通讯内容
            //PlaintextStreamFilter = (context, token) =>
            //{
            //    return ValueTask.FromResult(context.PlaintextStream);
            //}
        };

        return socketsHttpHandler;
    }

    #endregion

    /// <summary/>
    ~SegmentFileDownloaderByHttpClient()
    {
        Dispose();
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

    private Channel<DownloadData> DownloadDataList { get; }

    /// <summary>
    /// 被加入到 <see cref="DownloadDataList"/> 的下载数量
    /// </summary>
    private int _workTaskCount;

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
    /// <summary>
    /// 断点续传控制器，仅在有断点续传需求时才不为空
    /// </summary>
    private BreakpointResumptionTransmissionManager? BreakpointResumptionTransmissionManager { set; get; }
    private int _idGenerator;

    /// <summary>
    /// 每一次分段下载的超时时间，默认10秒
    /// </summary>
    public TimeSpan StepTimeOut { get; }
    /// <summary>
    /// 断点续传记录文件
    /// </summary>
    private FileInfo? BreakpointResumptionTransmissionRecordFile { get; }

    //变速器3秒为一周期
    private TimeSpan ControlDelayTime { get; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// 最大线程数量
    /// </summary>
    private int MaxThreadCount { get; } = 10;

    /// <summary>
    /// 用来决定需要多线程下载的资源的最小长度。如果小于此长度的资源，那就不需要多线程下载了
    /// </summary>
    private const int MinContentLengthNeedMultiThread = 1000;

    /// <summary>
    /// 下载资源的最后一部分，用来判断资源是否真的支持分段下载。下载最后一部分的下载长度
    /// </summary>
    private const int DownloadLastLength = 100;

    /// <summary>
    /// 网速控制开关
    /// </summary>
    /// <returns></returns>
    private async void ControlSwitch()
    {
        // 使用独立的线程的优势在于不需要等待下载就能进入方法
        // 可以干掉 LastTime 属性，因此定时是 3 秒
        await Task.Delay(ControlDelayTime).ConfigureAwait(false);

        while (!SegmentManager.IsFinished())
        {
            LogDebugInternal("Start ControlSwitch");
            var (segment, runCount, maxReportTime) = SegmentManager.GetMaxWaitReportTimeDownloadSegmentStatus();
            var waitCount = _workTaskCount;

            LogDebugInternal("ControlSwitch 当前等待数量：{0},待命最大响应时间：{1},运行数量：{2},运行线程{3}", waitCount, maxReportTime, runCount, _threadCount);

            if (_threadCount == 0 && maxReportTime > TimeSpan.FromSeconds(10))
            {
                // 如果跑着跑着，线程都休息了，那也应该多加点线程来跑
                // 不立刻开始，因为此时的网络应该有锅，等一等再开始
                LogDebugInternal("ControlSwitch StartDownloadTask. ThreadCount={0}", _threadCount);

                StartDownloadTask();
            }
            else if (maxReportTime > TimeSpan.FromSeconds(10) && segment != null && runCount > 1)
            {
                // 此时速度太慢
                segment.LoadingState = DownloadingState.Pause;
                LogDebugInternal("ControlSwitch slowly pause segment={0}", segment.Number);
            }
            else if (maxReportTime < TimeSpan.FromMilliseconds(600) && waitCount > 0 || runCount < 1)
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

            LogDebugInternal("Finish ControlSwitch");
            //变速器3秒为一周期
            await Task.Delay(ControlDelayTime).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 开启线程下载
    /// </summary>
    private void StartDownloadTask()
    {
        _ = Task.Run(DownloadTaskInner);

        async ValueTask DownloadTaskInner()
        {
            Interlocked.Increment(ref _threadCount);
            try
            {
                await DownloadTask().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                // 这里是后台线程的顶层了，应该接所有的异常
                _logger.LogError(e, $"[DownloadTaskInner] Throw unhandle exception. Type={e.GetType().FullName} Message={e.Message}");
                // 既然这里挂掉了，理论上需要补充一个线程才对。但是为了减少诡异的递归，将启动新线程的任务交给速度控制器 网速控制开关 的逻辑进行统一开启
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
    public async ValueTask DownloadFileAsync()
    {
        _logger.LogInformation($"Start download Url={Url} File={File.FullName}");

        var (response, contentLength) = await GetContentLength().ConfigureAwait(false);

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

        if (BreakpointResumptionTransmissionRecordFile is null)
        {
            // 没有断点续传
            SegmentManager = new SegmentManager(contentLength);
        }
        else
        {
            // 有断点续传
            var manager = new BreakpointResumptionTransmissionManager(BreakpointResumptionTransmissionRecordFile, FileWriter, contentLength);
            // 有断点续传情况下，先读取断点续传文件，通过此文件获取到需要下载的内容
            SegmentManager = manager.CreateSegmentManager();
            BreakpointResumptionTransmissionManager = manager;
        }

        _progress.Report(new DownloadProgress($"file length = {contentLength}", SegmentManager));

        var downloadSegment = SegmentManager.GetNewDownloadSegment();

        // 下载第一段
        Download(response, downloadSegment!);

        // 是否此资源支持被分段下载
        bool supportSegment;

        // 先判断下载的内容的长度，如果下载内容太小了，那就连分段都不用了
        if (contentLength < MinContentLengthNeedMultiThread)
        {
            supportSegment = false;
        }
        else
        {
            supportSegment = await TryDownloadLast(contentLength).ConfigureAwait(false);
        }

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

        await FileDownloadTask.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// 获取整个下载的长度
    /// </summary>
    /// <returns></returns>
    private async ValueTask<(HttpResponseMessage response, long contentLength)> GetContentLength()
    {
        _logger.LogInformation("开始获取整个下载长度");

        HttpResponseMessage? response = await GetHttpResponseMessageAsync().ConfigureAwait(false);

        if (response == null)
        {
            return default;
        }

        var contentLength = response.Content.Headers.ContentLength ?? 0;

        _logger.LogInformation(
            $"完成获取文件长度，文件长度 {contentLength} {contentLength / 1024}KB {contentLength / 1024.0 / 1024.0:0.00}MB");

        return (response, contentLength);
    }

    /// <summary>
    /// 通过 Url 创建出对应的 <see cref="HttpRequestMessage"/> 实例
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    protected virtual HttpRequestMessage CreateHttpRequestMessage(string url) => new HttpRequestMessage(HttpMethod.Get, url)
    {
        // 优先选用 2.0 版本，如果客户端（用户电脑上）或服务端不支持，会自动降级
        Version = HttpVersion.Version20,
    };

    /// <summary>
    /// 在 <see cref="HttpRequestMessage"/> 经过了应用设置之后调用，应用的设置包括下载的 Range 等值，调用这个方法之后的下一步将会是使用这个方法的返回值去下载文件
    /// </summary>
    /// <param name="httpRequestMessage"></param>
    /// <returns></returns>
    protected virtual HttpRequestMessage OnHttpRequestMessageSet(HttpRequestMessage httpRequestMessage) => httpRequestMessage;

    /// <summary>
    /// 这是给我自己开发调试用的
    /// </summary>
    /// <param name="message"></param>
    /// <param name="args"></param>
    [Conditional("DEBUG")]
    private void LogDebugInternal(string message, params object[] args)
    {
        _logger.LogDebug(message, args);
    }

    private async ValueTask<HttpResponseMessage?> GetHttpResponseMessageAsync(Action<HttpRequestMessage>? action = null)
    {
        var id = Interlocked.Increment(ref _idGenerator);

        for (var i = 0; !_isDisposed; i++)
        {
            TimeSpan retryDelayTime = TimeSpan.FromMilliseconds(100);

            try
            {
                var url = Url;
                LogDebugInternal("[GetHttpResponseMessageAsync] [{0}] Create HttpRequestMessage. Retry Count {0}", id, i);

                HttpRequestMessage httpRequestMessage = CreateHttpRequestMessage(url);

                LogDebugInternal("[GetHttpResponseMessageAsync] [{0}] Enter action.", id);
                action?.Invoke(httpRequestMessage);
                httpRequestMessage = OnHttpRequestMessageSet(httpRequestMessage);

                var stopwatch = Stopwatch.StartNew();
                LogDebugInternal("[GetHttpResponseMessageAsync] [{0}] Start GetResponseAsync.", id);
                var response = await GetResponseAsync(httpRequestMessage).ConfigureAwait(false);
                stopwatch.Stop();
                LogDebugInternal("[GetHttpResponseMessageAsync] [{0}] Finish GetResponseAsync. Cost time {1} ms", id,
                    stopwatch.ElapsedMilliseconds);

                return response;
            }
            catch (HttpRequestException e)
            {
                _logger.LogInformation($"[{id}] 第{i}次获取长度失败 {e}");

                if (e.InnerException is SocketException socketException)
                {
                    // 如果是找不到主机，那就不用继续下载了
                    if (socketException.ErrorCode == 11001)
                    {
                        // 不知道这样的主机
                        throw;
                    }
                }

                // 其他情况，再多等一会
                retryDelayTime = TimeSpan.FromSeconds(1);
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
            LogDebugInternal("[GetHttpResponseMessageAsync] [{0}] Delay {1} ms", id, retryDelayTime.TotalMilliseconds);
            await Task.Delay(retryDelayTime).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>
    /// 给继承的类可以从 <paramref name="request"/> 获取消息
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    protected virtual Task<HttpResponseMessage> GetResponseAsync(HttpRequestMessage request)
        => HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

    /// <summary>
    /// 尝试获取链接响应
    /// </summary>
    /// <param name="downloadSegment"></param>
    /// <returns></returns>
    private async ValueTask<HttpResponseMessage?> GetHttpResponseMessageAsync(DownloadSegment downloadSegment)
    {
        _logger.LogInformation(
            $"Start Get GetHttpResponseMessageAsync{downloadSegment.StartPoint}-{downloadSegment.CurrentDownloadPoint}/{downloadSegment.RequirementDownloadPoint}");

        // 为什么不使用 StartPoint 而是使用 CurrentDownloadPoint 是因为需要处理重试

        var response = await GetHttpResponseMessageAsync(httpRequestMessage =>
        {
            SetRange(httpRequestMessage, downloadSegment.CurrentDownloadPoint, downloadSegment.RequirementDownloadPoint);
        }).ConfigureAwait(false);
        return response;
    }

    private async ValueTask DownloadTask()
    {
        while (!SegmentManager.IsFinished())
        {
            // 不需要进行等待，就是开始下载
            DownloadData data;
            try
            {
                data = await DownloadDataList.Reader.ReadAsync().ConfigureAwait(false);
                Interlocked.Decrement(ref _workTaskCount);
            }
            catch (ChannelClosedException)
            {
                // 调用了 FinishDownload 表示完成
                return;
            }
            catch (OperationCanceledException)
            {
                // 也就是相当于完成了
                return;
            }
            // 没有内容了
            if (SegmentManager.IsFinished())
            {
                return;
            }

            var downloadSegment = data.DownloadSegment;
            downloadSegment.LoadingState = DownloadingState.Runing;

            _logger.LogInformation(
                $"Download {downloadSegment.StartPoint}-{downloadSegment.CurrentDownloadPoint}/{downloadSegment.RequirementDownloadPoint}");

            downloadSegment.Message = "Start GetHttpResponseMessage";

            try
            {
                using var response = data.HttpResponseMessage ?? await GetHttpResponseMessageAsync(downloadSegment).ConfigureAwait(false);
                downloadSegment.Message = "Finish GetHttpResponseMessage";

                if (response is not null)
                {
                    await DownloadSegmentInner(response, downloadSegment).ConfigureAwait(false);
                }
                else
                {
                    // 如果是空，那将在下面的逻辑放入消费，等待重新下载
                }
            }
            catch (Exception e)
            {
                // 已知异常列表
                // error System.IO.IOException:  Received an unexpected EOF or 0 bytes from the transport stream.
                // +		$exception	{"The operation was canceled."}	System.Threading.Tasks.TaskCanceledException
                // 由于方法的逻辑限制了范围，这里可以放心使用捕获所有异常

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

        await FinishDownload().ConfigureAwait(false);
    }

    /// <summary>
    /// 下载的主要逻辑
    /// </summary>
    /// <param name="response"></param>
    /// <param name="downloadSegment"></param>
    /// <returns></returns>
    /// 这个方法如果触发异常，将会在上一层进行重试
    private async ValueTask DownloadSegmentInner(HttpResponseMessage response, DownloadSegment downloadSegment)
    {
        downloadSegment.Message = "Start GetResponseStream";
        await using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
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
            downloadSegment.LastDownTime = DateTime.Now;
            var n = await responseStream.ReadAsync(buffer, 0, length, cancellationTokenSource.Token).ConfigureAwait(false);
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

    private async void Download(HttpResponseMessage? httpResponseMessage, DownloadSegment downloadSegment)
    {
        LogDebugInternal("[Download] Enqueue Download. {0}", downloadSegment);
        await DownloadDataList.Writer.WriteAsync(new DownloadData(httpResponseMessage, downloadSegment)).ConfigureAwait(false);
        Interlocked.Increment(ref _workTaskCount);
    }

    private void Download(DownloadSegment? downloadSegment)
    {
        if (downloadSegment == null)
        {
            return;
        }

        Download(null, downloadSegment);
    }

    private async ValueTask FinishDownload()
    {
        LogDebugInternal($"FinishDownload");

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

        await FileWriter.DisposeAsync().ConfigureAwait(false);
        await FileStream.DisposeAsync().ConfigureAwait(false);

        DownloadDataList.Writer.Complete();

        BreakpointResumptionTransmissionManager?.Dispose();
        // 默认下载完成删除断点续传文件
        try
        {
            if (BreakpointResumptionTransmissionRecordFile is not null && System.IO.File.Exists(BreakpointResumptionTransmissionRecordFile.FullName))
            {
                System.IO.File.Delete(BreakpointResumptionTransmissionRecordFile.FullName);
            }
        }
        catch
        {
            // 不给删除就不删除咯
        }

        FileDownloadTask.SetResult(true);
    }

    /// <summary>
    /// 尝试下载最末尾的部分，通过下载最末尾部分判断此服务是否真的支持分段下载功能
    /// </summary>
    /// <param name="contentLength"></param>
    /// <returns>true:此资源支持分段下载。false:此资源不支持分段下载</returns>
    /// 有些服务会在 Head 里面骗我说支持，实际上他是不支持的。试试下载最后一段的内容，再判断下载长度，即可知道服务是不是在骗我
    private async ValueTask<bool> TryDownloadLast(long contentLength)
    {
        // 尝试下载后部分，如果可以下载后续的 DownloadLastLength（100） 个字节，那么这个链接支持分段下载

        var startPoint = contentLength - DownloadLastLength;

        var responseLast = await GetHttpResponseMessageAsync(httpRequestMessage =>
        {
            var fromPoint = startPoint;
            var toPoint = contentLength;

            SetRange(httpRequestMessage, fromPoint, toPoint);
        }).ConfigureAwait(false);

        if (responseLast == null)
        {
            return false;
        }

        if (responseLast.Content.Headers.ContentLength == DownloadLastLength)
        {
            var downloadSegment = new DownloadSegment(startPoint, contentLength);
            SegmentManager.RegisterDownloadSegment(downloadSegment);

            Download(responseLast, downloadSegment);

            return true;
        }

        return false;
    }

    private static void SetRange(HttpRequestMessage httpRequestMessage, long fromPoint, long toPoint)
    {
        if (httpRequestMessage.Headers.Range?.Ranges is null)
        {
            httpRequestMessage.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue();
        }

        httpRequestMessage.Headers.Range.Ranges.Clear();
        httpRequestMessage.Headers.Range.Ranges.Add(new System.Net.Http.Headers.RangeItemHeaderValue(fromPoint, toPoint));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_shouldDisposeHttpClient)
        {
            HttpClient.Dispose();
        }
        GC.SuppressFinalize(this);
    }

    private class DownloadData
    {
        public DownloadData(HttpResponseMessage? httpResponseMessage, DownloadSegment downloadSegment)
        {
            HttpResponseMessage = httpResponseMessage;
            DownloadSegment = downloadSegment;
        }

        public HttpResponseMessage? HttpResponseMessage { get; }

        public DownloadSegment DownloadSegment { get; }
    }
}
