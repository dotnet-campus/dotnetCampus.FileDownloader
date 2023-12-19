using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.UI.Dispatching;

namespace UnoFileDownloader.Presentation
{
    public partial record AboutModel(INavigator Navigator)
    {
        public void CloseAbout()
        {
            _ = Navigator.NavigateBackAsync(this);
        }

        public void GotoGitHub()
        {
           var d = Dispatcher;

            _ = Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/dotnet-campus/dotnetCampus.FileDownloader"));
        }

        // 这是没有用的，返回的是空
        protected DispatcherQueue Dispatcher { get; } = DispatcherQueue.GetForCurrentThread();
    }
}
