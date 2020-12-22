using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;
using dotnetCampus.FileDownloader.Utils;
using Microsoft.Extensions.Logging;

namespace dotnetCampus.FileDownloader
{
    /// <summary>
    /// 文件下载辅助方法
    /// </summary>
    /// 这个类有点业务，但是我的几个项目都有差不多的逻辑，于是就放在这
    public static class FileDownloaderHelper
    {
        /// <summary>
        /// 异步下载文件
        /// </summary>
        /// <param name="url">下载链接，不对下载链接是否有效进行校对</param>
        /// <param name="file">下载的文件路径</param>
        /// <param name="logger">下载时的内部日志，默认将使用 Debug 输出</param>
        /// <param name="progress">下载进度</param>
        /// <param name="sharedArrayPool">共享缓存数组池，默认使用 ArrayPool 池</param>
        /// <param name="bufferLength">缓存的数组长度，默认是 65535 的长度</param>
        /// <param name="stepTimeOut">每一步 每一分段下载超时时间 默认 10 秒</param>
        /// <returns></returns>
        public static Task DownloadFileAsync(string url, FileInfo file,
            ILogger<SegmentFileDownloader>? logger = null,
            IProgress<DownloadProgress>? progress = null, ISharedArrayPool? sharedArrayPool = null,
            int bufferLength = ushort.MaxValue, TimeSpan? stepTimeOut = null)
        {
            var segmentFileDownloader = new SegmentFileDownloader(url, file, logger, progress, sharedArrayPool, bufferLength, stepTimeOut);

            return segmentFileDownloader.DownloadFileAsync();
        }

        /// <summary>
        /// 下载到某个文件夹，仅提供 Windows 下使用，也仅在 Windows 下经过测试
        /// </summary>
        /// <param name="url"></param>
        /// <param name="downloadFolder"></param>
        /// <param name="tempFolder">如不传，将使用 <paramref name="downloadFolder"/>作为下载存放的临时文件夹</param>
        /// <param name="logger">下载时的内部日志，默认将使用 Debug 输出</param>
        /// <param name="progress">下载进度</param>
        /// <param name="sharedArrayPool">共享缓存数组池，默认使用 ArrayPool 池</param>
        /// <param name="bufferLength">缓存的数组长度，默认是 65535 的长度</param>
        /// <param name="stepTimeOut">每一步 每一分段下载超时时间 默认 10 秒</param>
        /// <returns></returns>
        public static async Task<FileInfo> DownloadFileToFolderAsync(string url, DirectoryInfo downloadFolder,
            DirectoryInfo? tempFolder = null, ILogger<SegmentFileDownloader>? logger = null,
            IProgress<DownloadProgress>? progress = null, ISharedArrayPool? sharedArrayPool = null,
            int bufferLength = ushort.MaxValue, TimeSpan? stepTimeOut = null)
        {
#if !NETFRAMEWORK
            // 也许会在非 Windows 下系统使用
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new PlatformNotSupportedException($"当前方法仅供 Windows 系统使用");
            }
#endif

            downloadFolder.Create();
            tempFolder ??= downloadFolder;
            tempFolder.Create();

            // 创建临时文件
            var fileName = FileNameHelper.GuessFileNameFromUrl(url, fallbackName: Path.GetRandomFileName());

            var downloadFile = new FileInfo(Path.Combine(tempFolder.FullName, fileName));
            var segmentFileDownloader = new InnerSegmentFileDownloader(url, downloadFile, logger, progress, sharedArrayPool, bufferLength, stepTimeOut);
            await segmentFileDownloader.DownloadFileAsync();

            // 下载完成了之后，尝试移动文件夹
            // 优先使用服务器返回的文件名
            var finallyFileName = segmentFileDownloader.ServerSuggestionFileName;
            if (string.IsNullOrEmpty(finallyFileName))
            {
                finallyFileName = fileName;
            }

            var finallyFile = new FileInfo(Path.Combine(downloadFolder.FullName, finallyFileName));

            if (finallyFile.FullName.Equals(downloadFile.FullName, StringComparison.OrdinalIgnoreCase))
            {
                // 这里在 Windows 下可以忽略文件大小写等，但是在 Linux 下就不可以
                // 好在此方法当前仅给 Windows 使用
                return finallyFile;
            }

            if (finallyFile.Exists)
            {
                // 重新加个名字，理论上这个名字不会重叠
                finallyFile = new FileInfo(Path.Combine(finallyFile.Directory.FullName,
                    Path.GetFileNameWithoutExtension(finallyFile.FullName) + Path.GetRandomFileName() +
                    finallyFile.Extension));
            }

            if (Path.GetPathRoot(finallyFile.FullName) == Path.GetPathRoot(downloadFile.FullName))
            {
                // 相同的驱动器，移动就可以了
                // 在非 Windows 下有驱动器么？
                downloadFile.MoveTo(finallyFile.FullName);
            }
            else
            {
                // 谁这么无聊，用的下载文件和最终文件还不相同
                // 那就先复制一下再删除原来的文件
                downloadFile.CopyTo(finallyFile.FullName);
                downloadFile.Delete();
            }
            return finallyFile;
        }

        class InnerSegmentFileDownloader : SegmentFileDownloader
        {
            /// <param name="url">下载链接，不对下载链接是否有效进行校对</param>
            /// <param name="file">下载的文件路径</param>
            /// <param name="logger">下载时的内部日志，默认将使用 Debug 输出</param>
            /// <param name="progress">下载进度</param>
            /// <param name="sharedArrayPool">共享缓存数组池，默认使用 ArrayPool 池</param>
            /// <param name="bufferLength">缓存的数组长度，默认是 65535 的长度</param>
            /// <param name="stepTimeOut">每一步 每一分段下载超时时间 默认 10 秒</param>
            public InnerSegmentFileDownloader(string url, FileInfo file, ILogger<SegmentFileDownloader>? logger = null, IProgress<DownloadProgress>? progress = null, ISharedArrayPool? sharedArrayPool = null, int bufferLength = ushort.MaxValue, TimeSpan? stepTimeOut = null) : base(url, file, logger, progress, sharedArrayPool, bufferLength, stepTimeOut)
            {
            }

            /// <summary>
            /// 服务器端返回的文件名
            /// </summary>
            public string? ServerSuggestionFileName { get; private set; }

            protected override async Task<WebResponse> GetResponseAsync(WebRequest request)
            {
                var response = await request.GetResponseAsync();
                if (string.IsNullOrEmpty(ServerSuggestionFileName))
                {
                    ServerSuggestionFileName = WebResponseHelper.GetFileNameFromContentDisposition(response);
                }
                return response;
            }
        }

        /// <summary>
        /// 为文件名提供辅助方法。
        /// </summary>
        /// 以下代码从 https://github.com/walterlv/Walterlv.Packages 抄的
        static class FileNameHelper
        {
            private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

            /// <summary>
            /// 生成安全的文件名。字符串 <paramref name="text"/> 中的不合法字符将被替换成指定字符。
            /// </summary>
            /// <param name="text">要生成安全文件名的原始文件名。</param>
            /// <param name="replacement">当遇到不能成为文件名的字符的时候应该替换的字符。</param>
            /// <returns>安全的文件名。（不包含不合法的字符，但如果你的 <paramref name="text"/> 是空格，可能需要检查最终文件名是否是空白字符串。）</returns>
            public static string MakeSafeFileName(string text, char replacement = ' ')
            {
                var chars = text.ToCharArray();
                var invalidChars = InvalidFileNameChars;
                for (var i = 0; i < chars.Length; i++)
                {
                    for (var j = 0; j < invalidChars.Length; j++)
                    {
                        if (chars[i] == invalidChars[j])
                        {
                            chars[i] = replacement;
                            break;
                        }
                    }
                }
                return new string(chars);
            }

            /// <summary>
            /// 从 URL 中猜文件名。
            /// </summary>
            /// <param name="url">要猜测文件名的 URL 来源字符串。</param>
            /// <param name="limitedFileNameLength">如果需要，可以限制最终生成文件名的长度。</param>
            /// <param name="fallbackName">当无法猜出文件名，或文件名长度过长时，将取此名字。</param>
            /// <returns>猜出的文件名。</returns>
#if NETCOREAPP3_0 || NETCOREAPP3_1 || NETCOREAPP5_0 || NET5_0 || NET6_0
        [return: NotNullIfNotNull("fallbackName")]
#endif
            public static string? GuessFileNameFromUrl(string url, int? limitedFileNameLength = null, string? fallbackName = null)
            {
                var lastSlash = url.LastIndexOf('/') + 1;
                var lastQuery = url.IndexOf('?');
                if (lastSlash < 0)
                {
                    return fallbackName;
                }

                // 取 URL 中可能是文件名的部分。
                var name = lastQuery < 0 ? url.Substring(lastSlash) : url.Substring(lastSlash, lastQuery - lastSlash);

                // 对 URL 反转义。
                var unescapedName = Uri.UnescapeDataString(name);

                // 限制文件名长度。
                string? limitedFileName = limitedFileNameLength is null
                    ? unescapedName
                    : unescapedName.Length <= limitedFileNameLength.Value ? unescapedName : fallbackName;

                // 确保文件名字符是安全的。
                string? safeFileName = limitedFileName is null
                    ? limitedFileName
                    : FileNameHelper.MakeSafeFileName(limitedFileName);
                return safeFileName;
            }
        }
    }
}
