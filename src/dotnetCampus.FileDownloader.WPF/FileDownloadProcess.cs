using System;

namespace dotnetCampus.FileDownloader.WPF
{
    public class FileDownloadProcess : IProgress<DownloadProgress>
    {
        public FileDownloadProcess(DownloadFileInfo downloadFileInfo)
        {
            DownloadFileInfo = downloadFileInfo;
        }

        public void Report(DownloadProgress value)
        {


        }

        private DownloadFileInfo DownloadFileInfo { get; }
    }
}