using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using UnoFileDownloader.Business;

namespace UnoFileDownloader.Presentation
{
    public partial record NewTaskModel(INavigator Navigator, DownloadFileListManager DownloadFileListManager)
    {
        public string? DownloadSource { set; get; }
        public string? FileName { set; get; }

        public async Task StartDownloadAsync()
        {
            if (string.IsNullOrEmpty(DownloadSource))
            {
                return;
            }

            var filePath = FileName;

            if (string.IsNullOrEmpty(filePath))
            {
                filePath = Path.GetFileName(DownloadSource);
            }

            filePath = Path.GetFullPath(filePath);

            await DownloadFileListManager.AddDownloadFileAsync(new DownloadFileInfo()
            {
                AddedTime = DateTime.Now.ToString(format:null),
                DownloadUrl = DownloadSource,
                FileName = Path.GetFileName(filePath),
                FilePath = filePath,
            });

            // 似乎 NavigateViewModelAsync 跳转不回去，不知道为什么
            // await Navigator.NavigateViewModelAsync<MainModel>(this)
            var response = await Navigator.NavigateBackAsync(this);
            GC.KeepAlive(response);
        }

        public async Task CloseNewTask()
        {
            var response = await Navigator.NavigateBackAsync(this);
            if (response is null)
            {
                response = await Navigator.NavigateViewModelAsync<MainModel>(this);
            }
        }
    }
}
