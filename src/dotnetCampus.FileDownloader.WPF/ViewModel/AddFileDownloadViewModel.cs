using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using dotnetCampus.FileDownloader.WPF.Annotations;

namespace dotnetCampus.FileDownloader.WPF
{
    public class AddFileDownloadViewModel : INotifyPropertyChanged
    {
        public AddFileDownloadViewModel()
        {
            // 下面是测试使用的链接
#if DEBUG
            CurrentDownloadUrl = "https://download.visualstudio.microsoft.com/download/pr/c246f2b8-da39-4b12-b87d-bf89b6b51298/2d43d4ded4b6a0c4d1a0b52f0b9a3b30/dotnet-sdk-6.0.302-win-x64.exe";
            CurrentDownloadFilePath = "dotnet-sdk-6.0.302-win-x64.exe";
#endif
        }

        private string _currentDownloadFilePath = "";
        private string _currentDownloadUrl = "";

        // 这个版本 UI 没有跟上，先忽略参数判断
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

        // 这个版本 UI 没有跟上，先忽略参数判断
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

        public event PropertyChangedEventHandler PropertyChanged = delegate { };

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
}
