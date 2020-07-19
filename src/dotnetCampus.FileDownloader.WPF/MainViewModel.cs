namespace dotnetCampus.FileDownloader.WPF
{
    public class MainViewModel
    {
        public void Init()
        {

        }

        private DownloadFileManager DownloadFileManager { get; } = new DownloadFileManager();
    }
}