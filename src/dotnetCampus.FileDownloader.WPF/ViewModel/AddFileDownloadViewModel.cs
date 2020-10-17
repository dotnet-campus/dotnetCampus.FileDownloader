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
            CurrentDownloadUrl = "https://download.jetbrains.8686c.com/resharper/ReSharperUltimate.2020.1.3/JetBrains.ReSharperUltimate.2020.1.3.exe";
            CurrentDownloadFilePath = "JetBrains.ReSharperUltimate.2020.1.3.exe";
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
