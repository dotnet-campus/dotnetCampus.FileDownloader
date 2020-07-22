using System;
using System.Threading.Tasks;
using dotnetCampus.FileDownloader.Tool;

namespace dotnetCampus.FileDownloader.WPF
{
    /// <summary>
    /// 文件下载速度监控
    /// </summary>
    public class FileDownloadSpeedMonitor : IDisposable
    {
        /// <summary>
        /// 文件下载速度监控
        /// </summary>
        /// <param name="delayTime">触发事件延迟时间，默认是 500 毫秒</param>
        public FileDownloadSpeedMonitor(TimeSpan? delayTime = null)
        {
            if (delayTime is null)
            {
                DelayTime = TimeSpan.FromMilliseconds(500);
            }
        }

        public void Start()
        {
            _started = true;

            Task.Run(async () =>
            {
                while (_started)
                {
                    await Task.Delay(DelayTime);

                    if (!_started)
                    {
                        return;
                    }

                    if (_currentDownloadProgress is null)
                    {
                        continue;
                    }

                    var speed = GetCurrentSpeed();
                    var downloadInfoProgress = new DownloadInfoProgress(
                        FileSizeFormatter.FormatSize(_currentDownloadProgress.FileLength),
                        $"{FileSizeFormatter.FormatSize(_currentDownloadProgress.DownloadedLength)}/{FileSizeFormatter.FormatSize(_currentDownloadProgress.FileLength)}",
                        speed, _currentDownloadProgress);

                    ProgressChanged(this, downloadInfoProgress);

                    _lastDownloadLength = _currentDownloadProgress.DownloadedLength;
                }
            });
        }

        public void Stop()
        {
            _started = false;
        }

        public event EventHandler<DownloadInfoProgress> ProgressChanged = null!;

        private bool _started;

        private string GetCurrentSpeed()
        {
            var downloadedLength = _currentDownloadProgress!.DownloadedLength - _lastDownloadLength!.Value;

            var text =
                ($"{FileSizeFormatter.FormatSize(downloadedLength * 1000.0 / (DateTime.Now - _lastDateTime).TotalMilliseconds)}/s"
                );

            _lastDateTime = DateTime.Now;

            return text;
        }

        private TimeSpan DelayTime { get; }

        private DateTime _lastDateTime = DateTime.Now;

        public void Dispose()
        {
            _started = false;
        }

        public void Report(DownloadProgress downloadProgress)
        {
            _currentDownloadProgress = downloadProgress;
            _lastDownloadLength ??= downloadProgress.DownloadedLength;
        }

        private long? _lastDownloadLength;

        private DownloadProgress? _currentDownloadProgress;

        public class DownloadInfoProgress
        {
            public DownloadInfoProgress(string fileSize,
                string downloadProcess, string downloadSpeed, DownloadProgress downloadProgress)
            {
                FileSize = fileSize;
                DownloadProcess = downloadProcess;
                DownloadSpeed = downloadSpeed;
                DownloadProgress = downloadProgress;
            }

            public string FileSize { get; }
            public string DownloadProcess { get; }
            public string DownloadSpeed { get; }

            public DownloadProgress DownloadProgress { get; }
        }
    }
}