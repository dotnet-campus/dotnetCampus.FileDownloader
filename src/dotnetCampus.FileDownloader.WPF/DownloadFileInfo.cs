using System;

namespace dotnetCampus.FileDownloader.WPF
{
    public class DownloadFileInfo
    {
        public string FileName { get; set; }

        public DateTime AddedTime { get; set; }

        public string FileSize { get; set; }

        public string DownloadProcess { get; set; }

        public string DownloadSpeed { get; set; }
    }
}