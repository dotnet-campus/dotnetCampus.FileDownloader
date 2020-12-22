using System;
using System.Net;
using System.Net.Mime;
using System.Text.RegularExpressions;

namespace dotnetCampus.FileDownloader.Utils
{
    /// <summary>
    /// 提供 <see cref="WebResponse"/> 的辅助
    /// </summary>
    public static class WebResponseHelper
    {
        /// <summary>
        /// 从 <paramref name="response"/> 的 Headers 的 Content-Disposition 获取文件名
        /// </summary>
        /// <param name="response"></param>
        /// <returns></returns>
        public static string? GetFileNameFromContentDisposition(WebResponse response)
        {
            var contentDispositionText = response.Headers["Content-Disposition"];
            if (string.IsNullOrEmpty(contentDispositionText))
            {
                return null;
            }
            else
            {
                return GetFileNameFromContentDispositionText(contentDispositionText);
            }
        }

        /// <summary>
        /// 从 Content-Disposition 字符串获取文件名
        /// </summary>
        /// <param name="contentDispositionText"></param>
        /// <returns></returns>
        public static string? GetFileNameFromContentDispositionText(string contentDispositionText)
        {
            try
            {
                var contentDisposition = new ContentDisposition(contentDispositionText);

                var parameter = contentDisposition.Parameters["filename*"];
                if (!string.IsNullOrEmpty(parameter))
                {
                    var regex = new Regex(@"([\S\s]*)''([\S\s]*)");
                    var match = regex.Match(parameter);
                    if (match.Success)
                    {
                        var encodingText = match.Groups[1].Value;
                        var fileNameText = match.Groups[2].Value;

                        if (string.Equals("utf-8", encodingText, StringComparison.OrdinalIgnoreCase))
                        {
                            var unescapeDataString = Uri.UnescapeDataString(fileNameText);
                            return unescapeDataString;
                        }
                    }
                }

                var fileName = contentDisposition.FileName;
                return fileName;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
