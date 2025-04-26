using System;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.Logging;

namespace dotnetCampus.FileDownloader.Tool
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await (await Parser.Default.ParseArguments<DownloadOption>(args).WithParsedAsync(DownloadFileAsync)).WithNotParsedAsync(
                 async _ =>
                 {
#if DEBUG
                     var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });

                     var logger = loggerFactory.CreateLogger<SegmentFileDownloader>();

                     // https://www.speedtest.cn/
                     var url =
                          "https://speedtest1.gd.chinamobile.com.prod.hosts.ooklaserver.net:8080/download?size=25000000&r=0.2978374611691549";
                     url =
                         "https://download.jetbrains.8686c.com/resharper/ReSharperUltimate.2020.1.3/JetBrains.ReSharperUltimate.2020.1.3.exe";
                     //var md5 = "7d6bbeb6617a7c0b7e615098fca1b167";// resharper

                     //url = "http://localhost:5000";
                     url =
                         "https://dscache.tencent-cloud.cn/upload//ES_686_194-4f155229efeef75bb9c9a3995060c766dc0eac28.png";

                     var file = new FileInfo(@"File.txt");

                     var progress = new Progress<DownloadProgress>();

                     await FileDownloaderHelper.DownloadFileAsync(url, file, progress:progress);
#endif
                     await Task.Delay(100);
                 });
        }

        private static async Task DownloadFileAsync(DownloadOption option)
        {
            var output = option.Output ?? "";

            output = Path.GetFullPath(output);

            Console.WriteLine($"Download url = {option.Url}");
            Console.WriteLine($"Output = {output}");

            try
            {
                var loggerFactory = LoggerFactory.Create(builder => { });

                var logger = loggerFactory.CreateLogger<SegmentFileDownloader>();

                using var progress = new FileDownloadSpeedProgress();

                progress.ProgressChanged += (sender, downloadProgress) =>
                {
                    Console.Clear();

                    Console.WriteLine($"Download url = {option.Url}");
                    Console.WriteLine($"Output = {output}");

                    Console.WriteLine();

                    Console.WriteLine(
                        $"Process {downloadProgress.DownloadProcess} {downloadProgress.DownloadSpeed}");

                    foreach (var downloadSegment in downloadProgress.DownloadProgress.GetCurrentDownloadSegmentList())
                    {
                        Console.WriteLine(downloadSegment);
                    }
                };

                var file = new FileInfo(output);
                var url = option.Url;
                using var segmentFileDownloader = new SegmentFileDownloaderByHttpClient(url, file, httpClient:null, logger, progress);

                await segmentFileDownloader.DownloadFileAsync();

                //finished = true;
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e);
                Console.ResetColor();
            }
        }
    }
}
