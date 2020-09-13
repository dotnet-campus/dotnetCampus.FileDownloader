using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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

            var segmentFileDownloader = new SegmentFileDownloader(url, new FileInfo(file), logger, progress,
                sharedArrayPool: SharedArrayPool, bufferLength: FileDownloaderSharedArrayPool.BufferLength);
            await segmentFileDownloader.DownloadFileAsync();

            // 下载完成逻辑
            progress.Stop();

            downloadFileInfo.DownloadSpeed = "";
            downloadFileInfo.DownloadProcess = "完成";
            downloadFileInfo.IsFinished = true;

            _ = DownloadFileListManager.WriteDownloadedFileListToFileAsync(DownloadFileInfoList.ToList());

            // 后续优化多任务下载的时候的回收
            _ = Task.Delay(TimeSpan.FromSeconds(3))
                .ContinueWith(_ => ((FileDownloaderSharedArrayPool)SharedArrayPool).Clean());
        }

        private readonly ILoggerFactory _loggerFactory;

        public AddFileDownloadViewModel AddFileDownloadViewModel { get; } = new AddFileDownloadViewModel();

    }
}