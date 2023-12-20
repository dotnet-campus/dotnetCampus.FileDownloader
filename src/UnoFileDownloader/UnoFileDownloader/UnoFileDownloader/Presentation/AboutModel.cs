using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using Microsoft.UI.Dispatching;

using UnoFileDownloader.Utils;

namespace UnoFileDownloader.Presentation
{
    public partial record AboutModel(INavigator Navigator, IDispatcherQueueProvider DispatcherQueueProvider) : INotifyPropertyChanged
    {
        private string _appInfo = string.Empty;

        public string AppInfo
        {
            set
            {
                if (value == _appInfo)
                {
                    return;
                }

                _appInfo = value;
                OnPropertyChanged();
            }
            get => _appInfo;
        }

        public async Task CloseAbout()
        {
            var response = await Navigator.NavigateBackAsync(this);
            if (response is null)
            {
                response = await Navigator.NavigateViewModelAsync<NewTaskModel>(this);
            }
        }

        public void GotoGitHub()
        {
            Dispatcher.TryEnqueue(() =>
            {
                _ = Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/dotnet-campus/dotnetCampus.FileDownloader"));
            });

            Task.Run(async () =>
            {
                while (true)
                {
                    AppInfo = Path.GetRandomFileName();
                    await Task.Delay(100);
                }
            });
        }

        protected DispatcherQueue Dispatcher { get; } = DispatcherQueueProvider.Dispatcher;
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
