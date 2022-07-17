using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using dotnetCampus.FileDownloader.WPF.Model;
using dotnetCampus.FileDownloader.WPF.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;

namespace dotnetCampus.FileDownloader.WPF
{
    public class MainViewModel
    {
        public MainViewModel()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                //如果Debug模式下，速度不能达到极限，可以屏蔽下面代码，使用原生的Debug.WriteLine();
                builder.AddProvider(new DebugLoggerProvider());
#if DEBUG
                builder.SetMinimumLevel(LogLevel.Debug);
#endif
            });

            _loggerFactory = loggerFactory;
        }

        public async void Init()
        {
            var downloadedFileInfoList = await DownloadFileListManager.ReadDownloadedFileListAsync();

            if (downloadedFileInfoList != null)
            {
                DownloadFileInfoList.AddRange(downloadedFileInfoList);
            }
        }

        public ObservableCollection<DownloadFileInfo> DownloadFileInfoList { get; } = new ObservableCollection<DownloadFileInfo>();

        private DownloadFileListManager DownloadFileListManager { get; } = new DownloadFileListManager();

        private ISharedArrayPool SharedArrayPool { get; } = new FileDownloaderSharedArrayPool();

        private readonly HttpClient _httpClient = new HttpClient();

        public async void AddDownloadFile()
        {
            var url = AddFileDownloadViewModel.CurrentDownloadUrl;
            var file = AddFileDownloadViewModel.CurrentDownloadFilePath;

            var logger = _loggerFactory.CreateLogger<SegmentFileDownloader>();
            using var progress = new FileDownloadSpeedProgress();

            file = Path.GetFullPath(file);

            var downloadFileInfo = new DownloadFileInfo()
            {
                FileName = Path.GetFileName(file),
                AddedTime = DateTime.Now.ToString(),
                DownloadUrl = url,
                FilePath = file
            };

            DownloadFileInfoList.Add(downloadFileInfo);

            progress.ProgressChanged += (sender, downloadProgress) =>
            {
                downloadFileInfo.DownloadSpeed = downloadProgress.DownloadSpeed;
                downloadFileInfo.FileSize = downloadProgress.FileSize;
                downloadFileInfo.DownloadProcess = downloadProgress.DownloadProcess;
            };

            _ = DownloadFileListManager.WriteDownloadedFileListToFileAsync(DownloadFileInfoList.ToList());

            // 断点续传文件
            var breakPointResumptionTransmissionRecordFile = file + ".dat";

            var segmentFileDownloader = new SegmentFileDownloaderByHttpClient(url, new FileInfo(file), _httpClient, logger, progress,
                sharedArrayPool: SharedArrayPool, bufferLength: FileDownloaderSharedArrayPool.BufferLength, breakPointResumptionTransmissionRecordFile: new FileInfo(breakPointResumptionTransmissionRecordFile));
            CurrentSegmentFileDownloader = segmentFileDownloader;
            bool success = false;
            try
            {
                await segmentFileDownloader.DownloadFileAsync();
                success = true;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                Debugger.Launch();
                Debugger.Break();
            }

            // 下载完成逻辑
            progress.Stop();

            if (success)
            {
                downloadFileInfo.DownloadSpeed = "";
                downloadFileInfo.DownloadProcess = "完成";
            }
            else
            {
                downloadFileInfo.DownloadSpeed = "";
                downloadFileInfo.DownloadProcess = "失败";
            }
            downloadFileInfo.IsFinished = true;

            _ = DownloadFileListManager.WriteDownloadedFileListToFileAsync(DownloadFileInfoList.ToList());

            // 后续优化多任务下载的时候的回收
            _ = Task.Delay(TimeSpan.FromSeconds(3))
                .ContinueWith(_ => ((FileDownloaderSharedArrayPool) SharedArrayPool).Clean());
        }

        private SegmentFileDownloaderByHttpClient? CurrentSegmentFileDownloader { set; get; }

        private readonly ILoggerFactory _loggerFactory;

        public AddFileDownloadViewModel AddFileDownloadViewModel { get; } = new AddFileDownloadViewModel();

    }
}
