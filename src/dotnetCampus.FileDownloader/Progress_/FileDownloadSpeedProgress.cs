using System;
using System.Threading.Tasks;

namespace dotnetCampus.FileDownloader
{
    /// <summary>
    /// 接近用户层的下载进度，包含下载速度
    /// </summary>
    public class FileDownloadSpeedProgress : IProgress<DownloadProgress>, IDisposable
    {
        /// <summary>
        /// 文件下载速度监控
        /// </summary>
        /// <param name="delayTime">触发事件延迟时间，默认是 500 毫秒</param>
        public FileDownloadSpeedProgress(TimeSpan? delayTime = null)
        {
            if (delayTime is null)
            {
                DelayTime = TimeSpan.FromMilliseconds(500);
            }
        }

        public void Start()
        {
            if (_started)
            {
                return;
            }

            _started = true;

            Task.Run(async () => { await WatchProcess(); });
        }

        private async Task WatchProcess()
        {
            while (_started)
            {
                if (!_started)
                {
                    return;
                }

                if (_currentDownloadProgress is null)
                {
                    continue;
                }

                var downloadInfoProgress = GetDownloadInfoProgress();

                ProgressChanged(this, downloadInfoProgress);

                _lastDownloadLength = _currentDownloadProgress.DownloadedLength;

                if (downloadInfoProgress.IsFinished)
                {
                    Stop();
                }

                await Task.Delay(DelayTime);
            }
        }

        private DownloadInfoProgress GetDownloadInfoProgress()
        {
            string speed = GetCurrentSpeed();
            string fileSize = FileSizeFormatter.FormatSize(_currentDownloadProgress!.FileLength);

            string downloadProcess =
                $"{FileSizeFormatter.FormatSize(_currentDownloadProgress.DownloadedLength)}/{FileSizeFormatter.FormatSize(_currentDownloadProgress.FileLength)}";

            var downloadInfoProgress = new DownloadInfoProgress
            (
                fileSize,
                downloadProcess,
                speed,
                _currentDownloadProgress
            );
            return downloadInfoProgress;
        }

        public void Stop()
        {
            _started = false;
        }

        public event EventHandler<DownloadInfoProgress> ProgressChanged = delegate { };

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

        public async void Report(DownloadProgress downloadProgress)
        {
            _currentDownloadProgress = downloadProgress;
            _lastDownloadLength ??= downloadProgress.DownloadedLength;

            if (!_started)
            {
                _started = true;
                await WatchProcess();
            }
        }

        private long? _lastDownloadLength;

        private DownloadProgress? _currentDownloadProgress;

        /// <summary>
        /// 下载进度
        /// </summary>
        public class DownloadInfoProgress
        {
            /// <summary>
            /// 创建下载进度
            /// </summary>
            public DownloadInfoProgress(string fileSize,
                string downloadProcess, string downloadSpeed, DownloadProgress downloadProgress)
            {
                FileSize = fileSize;
                DownloadProcess = downloadProcess;
                DownloadSpeed = downloadSpeed;
                DownloadProgress = downloadProgress;
            }

            /// <summary>
            /// 是否下载完成
            /// </summary>
            public bool IsFinished => DownloadProgress.DownloadedLength >= DownloadProgress.FileLength;

            /// <summary>
            /// 文件大小
            /// </summary>
            public string FileSize { get; }

            /// <summary>
            /// 下载进度 1MB/2MB
            /// </summary>
            public string DownloadProcess { get; }

            /// <summary>
            /// 下载速度 1MB/s
            /// </summary>
            public string DownloadSpeed { get; }

            /// <summary>
            /// 下载进度原信息
            /// </summary>
            public DownloadProgress DownloadProgress { get; }
        }
    }
}
