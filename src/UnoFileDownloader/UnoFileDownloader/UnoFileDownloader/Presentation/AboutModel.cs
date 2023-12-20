using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.UI.Dispatching;

using UnoFileDownloader.Utils;

namespace UnoFileDownloader.Presentation
{
    public partial record AboutModel(INavigator Navigator, IDispatcherQueueProvider DispatcherQueueProvider)
    {
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
        }

        protected DispatcherQueue Dispatcher { get; } = DispatcherQueueProvider.Dispatcher;
    }
}
