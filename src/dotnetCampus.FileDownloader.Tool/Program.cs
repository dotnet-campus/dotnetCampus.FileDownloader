﻿using System;
using System.IO;
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

                     var segmentFileDownloader = new SegmentFileDownloader(url, file, logger);
                     await segmentFileDownloader.DownloadFile();
#endif
                });
        }

        private static async Task DownloadFileAsync(DownloadOption option)
        {
            try
            {
                var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });

                var logger = loggerFactory.CreateLogger<SegmentFileDownloader>();

                var file = new FileInfo(option.Output);
                var url = option.Url;

                var segmentFileDownloader = new SegmentFileDownloader(url, file, logger);
                await segmentFileDownloader.DownloadFile();
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