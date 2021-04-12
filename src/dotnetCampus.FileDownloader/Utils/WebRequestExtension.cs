using System;
using System.Globalization;
using System.Net;

namespace dotnetCampus.FileDownloader.Utils
{
    static class WebRequestExtension
    {
        public static void AddRange(this WebRequest webRequest, int from, int to)
        {
#if NET45
            // 在 NET45 下，只能通过 HttpWebRequest 的方式设置
            if (webRequest is HttpWebRequest httpWebRequest)
            {
                httpWebRequest.AddRange(from, to);
                return;
            }
            else
            {
                throw new ArgumentException($"仅支持给 HttpWebRequest 设置 Range 长度");
            }
#else
            webRequest.AddRange(HttpKnownHeaderNames.Bytes, from, to);
#endif
        }

        public static void AddRange(this WebRequest webRequest, long from, long to)
        {
#if NET45
            // 在 NET45 下，只能通过 HttpWebRequest 的方式设置
            if (webRequest is HttpWebRequest httpWebRequest)
            {
                httpWebRequest.AddRange(from, to);
                return;
            }
            else
            {
                throw new ArgumentException($"仅支持给 HttpWebRequest 设置 Range 长度");
            }
#else
            webRequest.AddRange(HttpKnownHeaderNames.Bytes, from, to);
#endif
        }

        public static void AddRange(this WebRequest webRequest, string rangeSpecifier, long from, long to)
        {
            if (rangeSpecifier == null)
            {
                throw new ArgumentNullException(nameof(rangeSpecifier));
            }

            if ((from < 0) || (to < 0))
            {
                throw new ArgumentOutOfRangeException(from < 0 ? nameof(from) : nameof(to), "Range 太小了");
            }

            if (from > to)
            {
                throw new ArgumentOutOfRangeException(nameof(from), "传入的 From 比 To 大");
            }

            if (!AddRange(webRequest, rangeSpecifier, from.ToString(NumberFormatInfo.InvariantInfo),
                to.ToString(NumberFormatInfo.InvariantInfo)))
            {
                throw new InvalidOperationException();
            }
        }

        public static void AddRange(this WebRequest webRequest, string rangeSpecifier, int range)
        {
#if NET45
            if (webRequest is HttpWebRequest httpWebRequest)
            {
                if (!rangeSpecifier.Equals(HttpKnownHeaderNames.Bytes, StringComparison.Ordinal))
                {
                    throw new ArgumentException($"仅支持给 HttpWebRequest 设置 {HttpKnownHeaderNames.Bytes} 单位的长度");
                }

                httpWebRequest.AddRange(range);
                return;
            }
            else
            {
                throw new ArgumentException($"仅支持给 HttpWebRequest 设置 Range 长度");
            }
#else
            webRequest.AddRange(rangeSpecifier, (long) range);
#endif
        }

        public static void AddRange(this WebRequest webRequest, string rangeSpecifier, long range)
        {
            if (rangeSpecifier == null)
            {
                throw new ArgumentNullException(nameof(rangeSpecifier));
            }

            if (!AddRange(webRequest, rangeSpecifier, range.ToString(NumberFormatInfo.InvariantInfo),
                (range >= 0) ? "" : null))
            {
                throw new InvalidOperationException();
            }
        }

        private static bool AddRange(WebRequest webRequest, string rangeSpecifier, string from, string? to)
        {
            var webHeaderCollection = webRequest.Headers;
            string? curRange = webHeaderCollection[HttpKnownHeaderNames.Range];

            if ((curRange == null) || (curRange.Length == 0))
            {
                curRange = rangeSpecifier + "=";
            }
            else
            {
                if (!string.Equals(curRange.Substring(0, curRange.IndexOf('=')), rangeSpecifier,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                curRange = string.Empty;
            }

            curRange += @from;
            if (to != null)
            {
                curRange += "-" + to;
            }

            webHeaderCollection[HttpKnownHeaderNames.Range] = curRange;
            return true;
        }

        internal static class HttpKnownHeaderNames
        {
            public const string Range = "Range";

            public const string Bytes = "bytes";
        }
    }
}
