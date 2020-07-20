using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using dotnetCampus.FileDownloader.Tool;
using Microsoft.Extensions.Logging;

namespace dotnetCampus.FileDownloader.WPF
{
    public class MainViewModel
    {
        public MainViewModel()
        {
            var loggerFactory = LoggerFactory.Create(builder => { });

            _loggerFactory = loggerFactory;
        }

        public async void Init()
        {
            var downloadedFileInfoList = await DownloadFileManager.ReadDownloadedFileList();

            DownloadFileInfoList.AddRange(downloadedFileInfoList);
        }

        public ObservableCollection<DownloadFileInfo> DownloadFileInfoList { get; } = new ObservableCollection<DownloadFileInfo>();

        private DownloadFileManager DownloadFileManager { get; } = new DownloadFileManager();



        public async void AddDownloadFile()
        {
            var url = AddFileDownloadViewModel.CurrentDownloadUrl;
            var file = AddFileDownloadViewModel.CurrentDownloadFilePath;

            var logger = _loggerFactory.CreateLogger<SegmentFileDownloader>();
            var progress = new Progress<DownloadProgress>();

            file = Path.GetFullPath(file);

            var downloadFileInfo = new DownloadFileInfo()
            {
                FileName = Path.GetFileName(file),
                AddedTime = DateTime.Now.ToString(),
                DownloadUrl = url,
                FilePath = file
            };

            DownloadFileInfoList.Add(downloadFileInfo);

            using var fileDownloadSpeedMonitor = new FileDownloadSpeedMonitor();
            fileDownloadSpeedMonitor.ProgressChanged += (sender, text) =>
            {
                downloadFileInfo.DownloadSpeed = text;
            };

            fileDownloadSpeedMonitor.Start();

            progress.ProgressChanged += (sender, downloadProgress) =>
            {
                downloadFileInfo.FileSize = FileSizeFormatter.FormatSize(downloadProgress.FileLength);
                downloadFileInfo.DownloadProcess = $"{FileSizeFormatter.FormatSize(downloadProgress.DownloadedLength)}/{FileSizeFormatter.FormatSize(downloadProgress.FileLength)}";

                // ReSharper disable once AccessToDisposedClosure
                fileDownloadSpeedMonitor.Report(downloadProgress.DownloadedLength);
            };

            _ = DownloadFileManager.WriteDownloadedFileListToFile(DownloadFileInfoList.ToList());

            var segmentFileDownloader = new SegmentFileDownloader(url, new FileInfo(file), logger, progress);
            await segmentFileDownloader.DownloadFile();

            downloadFileInfo.DownloadSpeed = "";

            _ = DownloadFileManager.WriteDownloadedFileListToFile(DownloadFileInfoList.ToList());
        }

        private readonly ILoggerFactory _loggerFactory;

        public AddFileDownloadViewModel AddFileDownloadViewModel { get; } = new AddFileDownloadViewModel();

    }
}