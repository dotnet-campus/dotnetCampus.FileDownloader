using CommandLine;

namespace dotnetCampus.FileDownloader.Tool
{
    public class DownloadOption
    {
        [Option('u', "Url", Required = true)]
        public string Url { get; set; }

        [Option('o', "output", Required = true)]
        public string Output { get; set; }
    }
}
