using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace dotnetCampus.FileDownloader.WPF
{
    public class MainViewModel
    {
        public async void Init()
        {
            var downloadedFileInfoList = await DownloadFileManager.ReadDownloadedFileList();

            DownloadFileInfoList.AddRange(downloadedFileInfoList);
        }

        public ObservableCollection<DownloadFileInfo> DownloadFileInfoList { get; } = new ObservableCollection<DownloadFileInfo>();

        private DownloadFileManager DownloadFileManager { get; } = new DownloadFileManager();
    }

    public static class ObservableCollectionExtension
    {
        public static void AddRange<T>(this ObservableCollection<T> observableCollection, IEnumerable<T> list)
        {
            foreach (var temp in list)
            {
                observableCollection.Add(temp);
            }
        }
    }
}