using System.Collections.ObjectModel;
using System.Xml.Linq;

using Uno.Extensions;

using UnoFileDownloader.Business;
using UnoFileDownloader.Utils;

namespace UnoFileDownloader.Presentation
{
    public partial record MainModel
    {
        private INavigator _navigator;
        private readonly DownloadFileListManager _downloadFileListManager;

        public MainModel(
            IStringLocalizer localizer,
            IOptions<AppConfig> appInfo,
            INavigator navigator, DownloadFileListManager downloadFileListManager)
        {
            _navigator = navigator;
            _downloadFileListManager = downloadFileListManager;
            Title = localizer["Main"];
            Title += $" - {localizer["ApplicationName"]}";
            Title += $" - {appInfo?.Value?.Environment}";

            UpdateDownloadFileInfoViewList();
        }

        public string? Title { get; }

        public ObservableCollection<DownloadFileInfo> DownloadFileInfoViewList { get; } =
            new ObservableCollection<DownloadFileInfo>();

        public IState<string> Name => State<string>.Value(this, () => string.Empty);

        public async Task GoToSecond()
        {
            var name = await Name;
            await _navigator.NavigateViewModelAsync<SecondModel>(this, data: new Entity(name!));
        }

        public async Task GotToNewTask()
        {
            await _navigator.NavigateViewModelAsync<NewTaskModel>(this);
        }

        public async Task GoToAbout()
        {
            await _navigator.NavigateViewModelAsync<AboutModel>(this);
        }

        private void UpdateDownloadFileInfoViewList()
        {
            DownloadFileInfoViewList.Clear();
            DownloadFileInfoViewList.AddRange(_downloadFileListManager.DownloadFileInfoList);
        }
    }
}
