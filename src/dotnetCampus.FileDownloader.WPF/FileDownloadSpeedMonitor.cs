using System;
using System.Threading.Tasks;
using dotnetCampus.FileDownloader.Tool;

namespace dotnetCampus.FileDownloader.WPF
{
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
}