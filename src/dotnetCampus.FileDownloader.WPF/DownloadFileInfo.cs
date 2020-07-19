using System.ComponentModel;
using System.Runtime.CompilerServices;
using dotnetCampus.FileDownloader.WPF.Annotations;

namespace dotnetCampus.FileDownloader.WPF
{
    public class DownloadFileInfo : INotifyPropertyChanged
    {
        private string _downloadProcess;
        private string _fileSize;
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

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}