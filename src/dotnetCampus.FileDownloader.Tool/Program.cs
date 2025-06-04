using System;
using System.IO;
using System.Net.Http;
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
                         "https://node-103-27-27-20.speedtest.cn:51090/download?size=25000000&r=0.625866791098137";
                     url =
                         "https://download.jetbrains.com/resharper/dotUltimate.2025.1/JetBrains.dotUltimate.2025.1.exe";
                     //var md5 = "7d6bbeb6617a7c0b7e615098fca1b167";// resharper

                     //url = "http://localhost:5000";

                     // 这里的 gitdl.cn 是 iFileProxy 离线下载工具的地址，这是一个非常好的工具。开源地址： https://git.linxi.info/xianglin_admin/iFileProxy
                     url = "https://gitdl.cn/https://github.com/srwi/EverythingToolbar/releases/download/1.5.2/EverythingToolbar-1.5.2.msi";

                     //url =
                     //    "https://down.pc.yyb.qq.com/pcyyb/packing/14e1e37f997f49a58d560ab97fa335aa/pcyyb_2702800040_installer.exe";
                     //url = "https://pm.myapp.com/invc/xfspeed/qqpcmgr/download/QQPCDownload320001.exe";
                     //// 这个地址带了 Content-Disposition 头，文件名是从这个头中获取的
                     url =
                         "https://sw.pcmgr.qq.com/2f472366ca30d8ac1ad4acb64c77d2ad/680c8f51/spcmgr/download/BaiduNetdisk_txgj1_7.50.0.132.exe";
                     //url = "https://pc-package.wpscdn.cn/wps/download/W.P.S.60.1955.exe";

                     var downloadFolder = new DirectoryInfo(@"DownloadFolder");

                     var progress = new Progress<DownloadProgress>();

                     await FileDownloaderHelper.DownloadFileToFolderAsync(url, downloadFolder, progress: progress);
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
                using var segmentFileDownloader = new SegmentFileDownloaderByHttpClient(url, file, httpClient: null, logger, progress);

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
