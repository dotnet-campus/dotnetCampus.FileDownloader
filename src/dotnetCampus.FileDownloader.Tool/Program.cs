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

                     var file = new FileInfo(@"File.txt");

                     var progress = new Progress<DownloadProgress>();

                     var segmentFileDownloader = new SegmentFileDownloader(url, file, logger, progress);
                     await segmentFileDownloader.DownloadFile();
#endif
                     await Task.Delay(100);
                 });
        }

        private static async Task DownloadFileAsync(DownloadOption option)
        {
            var output = option.Output;

            output = Path.GetFullPath(output);

            Console.WriteLine($"Download url = {option.Url}");
            Console.WriteLine($"Output = {output}");

            try
            {
                var loggerFactory = LoggerFactory.Create(builder => { });

                var logger = loggerFactory.CreateLogger<SegmentFileDownloader>();

                var file = new FileInfo(output);
                var url = option.Url;

                var progress = new Progress<DownloadProgress>();

                var obj = new object();
                DownloadProgress downloadProgress = null;

                progress.ProgressChanged += (sender, p) =>
                {
                    lock (obj)
                    {
                        downloadProgress = p;
                    }
                };

                bool finished = false;
                long lastLength = 0;
                DateTime lastTime = DateTime.Now;

                _ = Task.Run(async () =>
                {
                    while (!finished)
                    {
                        lock (obj)
                        {
                            if (downloadProgress == null)
                            {
                                continue;
                            }

                            Console.Clear();

                            Console.WriteLine($"Download url = {option.Url}");
                            Console.WriteLine($"Output = {output}");

                            Console.WriteLine(
                                $"Process {downloadProgress.DownloadedLength * 100.0 / downloadProgress.FileLength:0.00}");
                            Console.WriteLine($"{downloadProgress.DownloadedLength}/{downloadProgress.FileLength}");
                            Console.WriteLine();

                            Console.WriteLine();

                            Console.WriteLine($"{(downloadProgress.DownloadedLength - lastLength) * 1000.0 / (DateTime.Now - lastTime).TotalMilliseconds / 1024 / 1024:0.00} MB/s");

                            lastLength = downloadProgress.DownloadedLength;
                            lastTime = DateTime.Now;

                            foreach (var downloadSegment in downloadProgress.GetDownloadSegmentList())
                            {
                                Console.WriteLine(downloadSegment);
                            }
                        }

                        await Task.Delay(500);
                    }
                });

                var segmentFileDownloader = new SegmentFileDownloader(url, file, logger, progress);

                await segmentFileDownloader.DownloadFile();

                finished = true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

    public class DownloadOption
    {
        [Option('u', "Url", Required = true)]
        public string Url { get; set; }

        [Option('o', "output", Required = true)]
        public string Output { get; set; }
    }
}