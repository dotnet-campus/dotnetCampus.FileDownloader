using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using dotnetCampus.FileDownloader.WPF.Annotations;
using Newtonsoft.Json;

namespace dotnetCampus.FileDownloader.WPF
{
    public class DownloadFileInfo : INotifyPropertyChanged
    {
        private string _downloadProcess;
        private string _fileSize;
        private string _downloadSpeed;
        private bool _isFinished = false;
        public string FileName { get; set; }

        public string FileSize
        {
            get => _fileSize;
            set
            {
                if (value == _fileSize) return;
                _fileSize = value;
                OnPropertyChanged();
            }
        }

        public string DownloadProcess
        {
            get => _downloadProcess;
            set
            {
                if (value == _downloadProcess) return;
                _downloadProcess = value;
                OnPropertyChanged();
            }
        }

        public string AddedTime { get; set; }

        public string DownloadUrl { get; set; }

        public string FilePath { get; set; }

        [JsonIgnore]
        public string DownloadSpeed
        {
            get => _downloadSpeed;
            set
            {
                if (value == _downloadSpeed) return;
                _downloadSpeed = value;
                OnPropertyChanged();
            }
        }

        public bool IsFinished
        {
            get => _isFinished;
            set
            {
                if (value == _isFinished) return;
                _isFinished = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
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
}