using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using dotnetCampus.FileDownloader;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MSTest.Extensions.Contracts;

namespace FileDownloader.Tests;

[TestClass]
public class DownloadRangeTest
{
    [ContractTestCase]
    public void TestDownload()
    {
        "提供可下载内容长度小于 100 时，可以正常下载".Test(async () =>
        {
            var port = GetAvailablePort(IPAddress.Loopback);

            var builder = WebApplication.CreateBuilder();
            WebApplication webApplication = builder.Build();
            var url = $"http://127.0.0.1:{port}";
            webApplication.Urls.Add(url);

            const int Length = 20;

            webApplication.MapGet("/", (async (context) =>
            {
                context.Response.ContentLength = Length;

                for (int i = 0; i < Length; i++)
                {
                    // 这里使用异步有些亏，好在这是单元测试的代码
                    // 如果要开同步，需要设置 builder.WebHost.ConfigureKestrel(c => c.AllowSynchronousIO = true); 才能使用，否则将会抛出异常
                    await context.Response.Body.WriteAsync(new byte[] { (byte) i });
                }
            }));

            await webApplication.StartAsync();

            var file = new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

            var segmentFileDownloaderByHttpClient = new SegmentFileDownloaderByHttpClient(url, file);

            await segmentFileDownloaderByHttpClient.DownloadFileAsync();
        });
    }

    private static int GetAvailablePort(IPAddress ip)
    {
        using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(ip, 0));
        socket.Listen(1);
        var ipEndPoint = (IPEndPoint) socket.LocalEndPoint!;
        var port = ipEndPoint.Port;
        return port;
    }
}
