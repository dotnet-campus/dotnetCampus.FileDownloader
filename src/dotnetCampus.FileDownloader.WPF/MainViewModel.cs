using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using dotnetCampus.FileDownloader.Tool;
using dotnetCampus.FileDownloader.WPF.Annotations;
using Microsoft.Extensions.Logging;

namespace dotnetCampus.FileDownloader.WPF
{
    public class MainViewModel : INotifyPropertyChanged
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

        public string CurrentDownloadFilePath
        {
            get => _currentDownloadFilePath;
            set
            {
                if (value == _currentDownloadFilePath) return;
                _currentDownloadFilePath = value;
                OnPropertyChanged();
            }
        }

        public string CurrentDownloadUrl
        {
            get => _currentDownloadUrl;
            set
            {
                if (value == _currentDownloadUrl) return;
                _currentDownloadUrl = value;
                OnPropertyChanged();
            }
        }

        public async void AddDownloadFile()
        {
            var url = CurrentDownloadUrl;
            var file = CurrentDownloadFilePath;

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
        private string _currentDownloadFilePath;
        private string _currentDownloadUrl;
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            var dispatcher = Application.Current.Dispatcher;
            if (dispatcher.CheckAccess())
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            else
            {
                dispatcher.InvokeAsync(() =>
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                });
            }
        }
    }

    public class FileDownloadSpeedMonitor : IDisposable
    {
        public void Start()
        {
            _started = true;

            Task.Run(async () =>
            {
                while (_started)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500));

                    ProgressChanged(this, GetCurrentSpeed());
                }
            });
        }

        public void Stop()
        {
            _started = false;
        }

        public event EventHandler<string> ProgressChanged = null!;

        private bool _started;

        public void Report(long downloadedLength)
        {
            _currentDownloadedLength = downloadedLength;
        }

        private string GetCurrentSpeed()
        {
            var text = ($"{ FileSizeFormatter.FormatSize((_currentDownloadedLength - _lastDownloadedLength) * 1000.0 / (DateTime.Now - _lastDateTime).TotalMilliseconds)}/s");
            _lastDateTime = DateTime.Now;
            _lastDownloadedLength = _currentDownloadedLength;

            return text;
        }

        private long _currentDownloadedLength;
        private long _lastDownloadedLength;
        private DateTime _lastDateTime;

        public void Dispose()
        {
            _started = false;
        }
    }

    public class FileDownloadProcess : IProgress<DownloadProgress>
    {
        public FileDownloadProcess(DownloadFileInfo downloadFileInfo)
        {
            DownloadFileInfo = downloadFileInfo;
        }

        public void Report(DownloadProgress value)
        {


        }

        private DownloadFileInfo DownloadFileInfo { get; }
    }

    public static class ObservableCollectionExtension
    {
        public static void AddRange<T>(this ObservableCollection<T> observableCollection, IEnumerable<T> list)
        {
            foreach (var temp in list)
            {
                observableCollection.Add(temp);
            }
        }
    }
}