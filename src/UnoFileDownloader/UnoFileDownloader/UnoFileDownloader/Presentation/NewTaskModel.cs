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

            await Navigator.NavigateViewModelAsync<MainModel>(this);
        }
    }
}
