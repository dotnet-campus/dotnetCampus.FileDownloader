﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

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
    /// <param name="breakpointResumptionTransmissionRecordFile">断点续下的信息记录文件，如为空将不带上断点续下功能</param>
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

        if (string.IsNullOrEmpty(url))
        {
            throw new ArgumentNullException(nameof(url));
        }

        Url = url;
        File = file ?? throw new ArgumentNullException(nameof(file));

        _logger.BeginScope("Url={url} File={file}", url, file);

        BufferLength = bufferLength;

        _shouldDisposeHttpClient = httpClient is null;
        HttpClient = httpClient ?? new HttpClient();

        DownloadDataList = Channel.CreateUnbounded<DownloadData>(new UnboundedChannelOptions()
        {
            SingleReader = false,
            AllowSynchronousContinuations = true,
        });
    }

    /// <summary>
    /// 是否需要释放 HttpClient 对象。如果是外部传入的，那就不需要释放，交给外部去进行释放
    /// </summary>
    private readonly bool _shouldDisposeHttpClient;

    private HttpClient HttpClient { get; }

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
            LogDebugInternal("Start ControlSwitch");
            var (segment, runCount, maxReportTime) = SegmentManager.GetDownloadSegmentStatus();
            var waitCount = _workTaskCount;

            LogDebugInternal("ControlSwitch 当前等待数量：{0},待命最大响应时间：{1},运行数量：{2},运行线程{3}", waitCount, maxReportTime, runCount, _threadCount);

            if (maxReportTime > TimeSpan.FromSeconds(10) && segment != null && runCount > 1)
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
            await Task.Delay(ControlDelayTime);
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
            await DownloadTask();
            Interlocked.Decrement(ref _threadCount);
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
    private async ValueTask<(HttpResponseMessage response, long contentLength)> GetContentLength()
    {
        _logger.LogInformation("开始获取整个下载长度");

        HttpResponseMessage? response = await GetHttpResponseMessageAsync();

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
    protected virtual HttpRequestMessage CreateHttpRequestMessage(string url) => new HttpRequestMessage(HttpMethod.Get, url);

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
                LogDebugInternal("[GetWebResponseAsync] [{0}] Create HttpRequestMessage. Retry Count {0}", id, i);

                HttpRequestMessage httpRequestMessage = CreateHttpRequestMessage(url);

                LogDebugInternal("[GetWebResponseAsync] [{0}] Enter action.", id);
                action?.Invoke(httpRequestMessage);
                httpRequestMessage = OnHttpRequestMessageSet(httpRequestMessage);

                var stopwatch = Stopwatch.StartNew();
                LogDebugInternal("[GetWebResponseAsync] [{0}] Start GetResponseAsync.", id);
                var response = await GetResponseAsync(httpRequestMessage);
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
    protected virtual Task<HttpResponseMessage> GetResponseAsync(HttpRequestMessage request)
        => HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

    /// <summary>
    /// 尝试获取链接响应
    /// </summary>
    /// <param name="downloadSegment"></param>
    /// <returns></returns>
    private async ValueTask<HttpResponseMessage?> GetWebResponse(DownloadSegment downloadSegment)
    {
        _logger.LogInformation(
            $"Start Get WebResponse{downloadSegment.StartPoint}-{downloadSegment.CurrentDownloadPoint}/{downloadSegment.RequirementDownloadPoint}");

        // 为什么不使用 StartPoint 而是使用 CurrentDownloadPoint 是因为需要处理重试

        var response = await GetHttpResponseMessageAsync(httpRequestMessage =>
        {
            SetRange(httpRequestMessage, downloadSegment.CurrentDownloadPoint, downloadSegment.RequirementDownloadPoint);
        });
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
                data = await DownloadDataList.Reader.ReadAsync();
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
    private async ValueTask DownloadSegmentInner(HttpResponseMessage? response, DownloadSegment downloadSegment)
    {
        if (response == null)
        {
            // 继续下一次
            throw new WebResponseException("Can not response");
        }

        downloadSegment.Message = "Start GetResponseStream";
        await using var responseStream = await response.Content.ReadAsStreamAsync();
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

    private async void Download(HttpResponseMessage? webResponse, DownloadSegment downloadSegment)
    {
        LogDebugInternal("[Download] Enqueue Download. {0}", downloadSegment);
        await DownloadDataList.Writer.WriteAsync(new DownloadData(webResponse, downloadSegment));
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

        await FileWriter.DisposeAsync();
        await FileStream.DisposeAsync();

        DownloadDataList.Writer.Complete();

        FileDownloadTask.SetResult(true);
    }

    private async ValueTask<bool> TryDownloadLast(long contentLength)
    {
        // 尝试下载后部分，如果可以下载后续的 100 个字节，那么这个链接支持分段下载
        const int downloadLength = 100;

        var startPoint = contentLength - downloadLength;

        var responseLast = await GetHttpResponseMessageAsync(httpRequestMessage =>
        {
            var fromPoint = startPoint;
            var toPoint = contentLength;

            SetRange(httpRequestMessage, fromPoint, toPoint);
        });

        if (responseLast == null)
        {
            return false;
        }

        if (responseLast.Content.Headers.ContentLength == downloadLength)
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
        public DownloadData(HttpResponseMessage? webResponse, DownloadSegment downloadSegment)
        {
            WebResponse = webResponse;
            DownloadSegment = downloadSegment;
        }

        public HttpResponseMessage? WebResponse { get; }

        public DownloadSegment DownloadSegment { get; }
    }
}
