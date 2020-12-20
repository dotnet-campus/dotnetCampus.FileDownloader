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
            Uri uri = new Uri(url);
            var unescapeDataString = Uri.UnescapeDataString(uri.AbsolutePath);
            var fileName = Path.GetFileName(unescapeDataString);

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
                    Path.GetFileNameWithoutExtension(finallyFile.FullName), Path.GetRandomFileName(),
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

        /// <summary>
        /// 包含对文件夹操作的封装，避免在业务中过多关注异常、跨驱动器、递归等本不希望关心的问题。
        /// </summary>
        /// 以下代码从 https://github.com/walterlv/Walterlv.Packages 抄的
        static class PackageDirectory
        {
            /// <summary>
            /// 将源路径文件夹移动成为目标路径文件夹。
            /// 由 <paramref name="overwrite"/> 参数指定在目标文件夹存在时应该覆盖还是将源文件夹全部删除。
            /// </summary>
            /// <param name="sourceDirectory">源文件夹。</param>
            /// <param name="targetDirectory">目标文件夹。</param>
            /// <returns>包含执行成功和失败的信息，以及中间执行中方法自动决定的一些细节。</returns>
            public static void Move(string sourceDirectory, string targetDirectory) => Move(
                VerifyDirectoryArgument(sourceDirectory, nameof(sourceDirectory)),
                VerifyDirectoryArgument(targetDirectory, nameof(targetDirectory)));

            /// <summary>
            /// 将源路径文件夹移动成为目标路径文件夹。
            /// 由 <paramref name="overwrite"/> 参数指定在目标文件夹存在时应该覆盖还是将源文件夹全部删除。
            /// </summary>
            /// <param name="sourceDirectory">源文件夹。</param>
            /// <param name="targetDirectory">目标文件夹。</param>
            /// <param name="overwrite">指定当目标路径存在现成现成文件夹时，应该如何覆盖目标文件夹。</param>
            /// <returns>包含执行成功和失败的信息，以及中间执行中方法自动决定的一些细节。</returns>
            public static IOResult Move(string sourceDirectory, string targetDirectory, DirectoryOverwriteStrategy overwrite = DirectoryOverwriteStrategy.Overwrite) => Move(
                VerifyDirectoryArgument(sourceDirectory, nameof(sourceDirectory)),
                VerifyDirectoryArgument(targetDirectory, nameof(targetDirectory)),
                overwrite);

            /// <summary>
            /// 将源路径文件夹复制成为目标路径文件夹。
            /// 由 <paramref name="overwrite"/> 参数指定在目标文件夹存在时应该覆盖还是将源文件夹全部删除。
            /// </summary>
            /// <param name="sourceDirectory">源文件夹。</param>
            /// <param name="targetDirectory">目标文件夹。</param>
            /// <param name="overwrite">是否覆盖。如果覆盖，那么目标文件夹中的原有文件将全部删除。</param>
            /// <returns>包含执行成功和失败的信息，以及中间执行中方法自动决定的一些细节。</returns>
            public static IOResult Copy(string sourceDirectory, string targetDirectory, bool overwrite) => Copy(
                VerifyDirectoryArgument(sourceDirectory, nameof(sourceDirectory)),
                VerifyDirectoryArgument(targetDirectory, nameof(targetDirectory)),
                overwrite ? DirectoryOverwriteStrategy.Overwrite : DirectoryOverwriteStrategy.DoNotOverwrite);

            /// <summary>
            /// 将源路径文件夹复制成为目标路径文件夹。
            /// 由 <paramref name="overwrite"/> 参数指定在目标文件夹存在时应该覆盖还是将源文件夹全部删除。
            /// </summary>
            /// <param name="sourceDirectory">源文件夹。</param>
            /// <param name="targetDirectory">目标文件夹。</param>
            /// <param name="overwrite">指定当目标路径存在现成现成文件夹时，应该如何覆盖目标文件夹。</param>
            /// <returns>包含执行成功和失败的信息，以及中间执行中方法自动决定的一些细节。</returns>
            public static IOResult Copy(string sourceDirectory, string targetDirectory, DirectoryOverwriteStrategy overwrite = DirectoryOverwriteStrategy.Overwrite) => Copy(
                VerifyDirectoryArgument(sourceDirectory, nameof(sourceDirectory)),
                VerifyDirectoryArgument(targetDirectory, nameof(targetDirectory)),
                overwrite);

            /// <summary>
            /// 删除指定路径的文件夹，此操作会递归删除文件夹内的所有文件，最后删除此文件夹自身。
            /// 如果目标文件夹是个连接点（Junction Point, Symbolic Link），则只会删除连接点而已，不会删除连接点所指目标文件夹中的文件。
            /// </summary>
            /// <param name="directory">要删除的文件夹。</param>
            /// <returns>包含执行成功和失败的信息，以及中间执行中方法自动决定的一些细节。</returns>
            public static IOResult Delete(string directory) => Delete(
                VerifyDirectoryArgument(directory, nameof(directory)));




            /// <summary>
            /// 将源路径文件夹移动成为目标路径文件夹。
            /// 由 <paramref name="overwrite"/> 参数指定在目标文件夹存在时应该覆盖还是将源文件夹全部删除。
            /// </summary>
            /// <param name="sourceDirectory">源文件夹。</param>
            /// <param name="targetDirectory">目标文件夹。</param>
            /// <param name="overwrite">是否覆盖。如果覆盖，那么目标文件夹中的原有文件将全部删除。</param>
            /// <returns>包含执行成功和失败的信息，以及中间执行中方法自动决定的一些细节。</returns>
            public static IOResult Move(DirectoryInfo sourceDirectory, DirectoryInfo targetDirectory, bool overwrite) => Move(
                sourceDirectory, targetDirectory,
                overwrite ? DirectoryOverwriteStrategy.Overwrite : DirectoryOverwriteStrategy.DoNotOverwrite);

            /// <summary>
            /// 将源路径文件夹移动成为目标路径文件夹。
            /// 由 <paramref name="overwrite"/> 参数指定在目标文件夹存在时应该覆盖还是将源文件夹全部删除。
            /// </summary>
            /// <param name="sourceDirectory">源文件夹。</param>
            /// <param name="targetDirectory">目标文件夹。</param>
            /// <param name="overwrite">指定当目标路径存在现成现成文件夹时，应该如何覆盖目标文件夹。</param>
            /// <returns>包含执行成功和失败的信息，以及中间执行中方法自动决定的一些细节。</returns>
            public static void Move(DirectoryInfo sourceDirectory, DirectoryInfo targetDirectory)
            {
                if (sourceDirectory is null)
                {
                    throw new ArgumentNullException(nameof(sourceDirectory));
                }

                if (targetDirectory is null)
                {
                    throw new ArgumentNullException(nameof(targetDirectory));
                }


                {
                    var deleteOverwriteResult = DeleteIfOverwrite(targetDirectory, overwrite);
                    logger.Append(deleteOverwriteResult);

                    try
                    {
                        logger.Log("无论是否存在，都创建文件夹。");
                        Directory.CreateDirectory(targetDirectory.FullName);

                        foreach (var file in sourceDirectory.EnumerateFiles())
                        {
                            var targetFilePath = Path.Combine(targetDirectory.FullName, file.Name);
                            file.MoveTo(targetFilePath);
                        }

                        foreach (DirectoryInfo directory in sourceDirectory.EnumerateDirectories())
                        {
                            var targetDirectoryPath = Path.Combine(targetDirectory.FullName, directory.Name);
                            var moveResult = Move(directory, new DirectoryInfo(targetDirectoryPath));
                            logger.Append(moveResult);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Fail(ex);
                        return logger;
                    }
                }
                else
                {
                    logger.Log("源目录与目标目录不在相同驱动器，先进行复制，再删除源目录。");

                    var copyResult = Copy(sourceDirectory, targetDirectory);
                    logger.Append(copyResult);

                    var deleteResult = Delete(sourceDirectory);
                    logger.Append(deleteResult);
                }
                return logger;
            }

            /// <summary>
            /// 将源路径文件夹复制成为目标路径文件夹。
            /// 由 <paramref name="overwrite"/> 参数指定在目标文件夹存在时应该覆盖还是将源文件夹全部删除。
            /// </summary>
            /// <param name="sourceDirectory">源文件夹。</param>
            /// <param name="targetDirectory">目标文件夹。</param>
            /// <param name="overwrite">是否覆盖。如果覆盖，那么目标文件夹中的原有文件将全部删除。</param>
            /// <returns>包含执行成功和失败的信息，以及中间执行中方法自动决定的一些细节。</returns>
            public static IOResult Copy(DirectoryInfo sourceDirectory, DirectoryInfo targetDirectory, bool overwrite) => Copy(
                sourceDirectory, targetDirectory,
                overwrite ? DirectoryOverwriteStrategy.Overwrite : DirectoryOverwriteStrategy.DoNotOverwrite);

            /// <summary>
            /// 将源路径文件夹复制成为目标路径文件夹。
            /// 由 <paramref name="overwrite"/> 参数指定在目标文件夹存在时应该覆盖还是将源文件夹全部删除。
            /// </summary>
            /// <param name="sourceDirectory">源文件夹。</param>
            /// <param name="targetDirectory">目标文件夹。</param>
            /// <param name="overwrite">指定当目标路径存在现成现成文件夹时，应该如何覆盖目标文件夹。</param>
            /// <returns>包含执行成功和失败的信息，以及中间执行中方法自动决定的一些细节。</returns>
            public static IOResult Copy(DirectoryInfo sourceDirectory, DirectoryInfo targetDirectory, DirectoryOverwriteStrategy overwrite = DirectoryOverwriteStrategy.Overwrite)
            {
                if (sourceDirectory is null)
                {
                    throw new ArgumentNullException(nameof(sourceDirectory));
                }

                if (targetDirectory is null)
                {
                    throw new ArgumentNullException(nameof(targetDirectory));
                }

                var logger = new IOResult();
                logger.Log($"复制目录，从“{sourceDirectory.FullName}”到“{targetDirectory.FullName}”。");

                if (!Directory.Exists(sourceDirectory.FullName))
                {
                    logger.Log($"要复制的源目录“{sourceDirectory.FullName}”不存在。");
                    return logger;
                }

                var deleteOverwriteResult = DeleteIfOverwrite(targetDirectory, overwrite);
                logger.Append(deleteOverwriteResult);

                try
                {
                    logger.Log("无论是否存在，都创建文件夹。");
                    Directory.CreateDirectory(targetDirectory.FullName);

                    foreach (var file in sourceDirectory.EnumerateFiles())
                    {
                        var targetFilePath = Path.Combine(targetDirectory.FullName, file.Name);
                        file.CopyTo(targetFilePath, true);
                    }

                    foreach (DirectoryInfo directory in sourceDirectory.EnumerateDirectories())
                    {
                        var targetDirectoryPath = Path.Combine(targetDirectory.FullName, directory.Name);
                        var copyResult = Copy(directory, new DirectoryInfo(targetDirectoryPath));
                        logger.Append(copyResult);
                    }
                }
                catch (Exception ex)
                {
                    logger.Fail(ex);
                    return logger;
                }

                return logger;
            }

            /// <summary>
            /// 删除指定路径的文件夹，此操作会递归删除文件夹内的所有文件，最后删除此文件夹自身。
            /// 如果目标文件夹是个连接点（Junction Point, Symbolic Link），则只会删除连接点而已，不会删除连接点所指目标文件夹中的文件。
            /// </summary>
            /// <param name="directory">要删除的文件夹。</param>
            /// <returns>包含执行成功和失败的信息，以及中间执行中方法自动决定的一些细节。</returns>
            public static IOResult Delete(DirectoryInfo directory)
            {
                if (directory is null)
                {
                    throw new ArgumentNullException(nameof(directory));
                }

                var logger = new IOResult();
                logger.Log($"删除目录“{directory.FullName}”。");

                if (JunctionPoint.Exists(directory.FullName))
                {
                    JunctionPoint.Delete(directory.FullName);
                }
                else if (!Directory.Exists(directory.FullName))
                {
                    logger.Log($"要删除的目录“{directory.FullName}”不存在。");
                    return logger;
                }

                Delete(directory, 0, logger);

                static void Delete(DirectoryInfo directory, int depth, IOResult logger)
                {
                    if (JunctionPoint.Exists(directory.FullName))
                    {
                        JunctionPoint.Delete(directory.FullName);
                    }
                    else if (!Directory.Exists(directory.FullName))
                    {
                        return;
                    }

                    try
                    {
                        foreach (var fileInfo in directory.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
                        {
                            File.Delete(fileInfo.FullName);
                        }

                        foreach (var directoryInfo in directory.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
                        {
                            var back = string.Join("\\", Enumerable.Repeat("..", depth));
                            Delete(directoryInfo, depth + 1, logger);
                        }

                        Directory.Delete(directory.FullName);
                    }
                    catch (Exception ex)
                    {
                        logger.Fail(ex);
                    }
                }

                return logger;
            }

            /// <summary>
            /// 创建一个目录联接（Junction Point），并连接到 <paramref name="targetDirectory"/> 指向的路径。
            /// </summary>
            /// <param name="linkDirectory">连接点的路径。</param>
            /// <param name="targetDirectory">要连接的目标文件夹。</param>
            /// <param name="overwrite">如果要创建的连接点路径已经存在连接点或者文件夹，则指定是否删除这个现有的连接点或文件夹。</param>
            /// <returns>包含执行成功和失败的信息，以及中间执行中方法自动决定的一些细节。</returns>
            public static IOResult Link(DirectoryInfo linkDirectory, DirectoryInfo targetDirectory, bool overwrite = true)
            {
                var logger = new IOResult();
                logger.Log($"创建目录联接，将“{linkDirectory.FullName}”联接到“{targetDirectory.FullName}”。");

                try
                {
                    JunctionPoint.Create(linkDirectory.FullName, targetDirectory.FullName, overwrite);
                }
                catch (Exception ex)
                {
                    logger.Fail(ex);
                }

                return logger;
            }

            private static IOResult DeleteIfOverwrite(DirectoryInfo targetDirectory, DirectoryOverwriteStrategy overwrite)
            {
                var logger = new IOResult();
                if (Directory.Exists(targetDirectory.FullName))
                {
                    switch (overwrite)
                    {
                        case DirectoryOverwriteStrategy.DoNotOverwrite:
                            {
                                logger.Log("目标目录已经存在，但是要求不被覆盖，抛出异常。");
                                throw new IOException($"目标目录“{targetDirectory.FullName}”已经存在，如要覆盖，请设置 {nameof(overwrite)} 为 true。");
                            }
                        case DirectoryOverwriteStrategy.Overwrite:
                            {
                                logger.Log("目标目录已经存在，删除。");
                                var deleteResult = Delete(targetDirectory.FullName);
                                logger.Append(deleteResult);
                            }
                            break;
                        case DirectoryOverwriteStrategy.MergeOverwrite:
                            {
                                // 如果是合并式覆盖，那么不需要删除，也不需要抛异常，直接覆盖即可。
                            }
                            break;
                        default:
                            break;
                    }
                }
                return logger;
            }

            [ContractArgumentValidator]
            private static DirectoryInfo VerifyDirectoryArgument(string directory, string argumentName)
            {
                if (directory is null)
                {
                    throw new ArgumentNullException(argumentName);
                }

                if (string.IsNullOrWhiteSpace(directory))
                {
                    throw new ArgumentException("不允许使用空字符串作为目录。", argumentName);
                }

                return new DirectoryInfo(directory);
            }
        }
    }
}
