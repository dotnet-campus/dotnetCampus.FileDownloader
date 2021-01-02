using System;
using System.Globalization;
using System.Net;

namespace dotnetCampus.FileDownloader.Utils
{
    static class WebRequestExtension
    {
        public static void AddRange(this WebRequest webRequest, int from, int to)
        {
            webRequest.AddRange("bytes", from, to);
        }


        public static void AddRange(this WebRequest webRequest, long from, long to)
        {
            webRequest.AddRange("bytes", from, to);
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

            if (!AddRange(webRequest, rangeSpecifier, from.ToString(NumberFormatInfo.InvariantInfo), to.ToString(NumberFormatInfo.InvariantInfo)))
            {
                throw new InvalidOperationException();
            }
        }

        public static void AddRange(this WebRequest webRequest, string rangeSpecifier, int range)
        {
            webRequest.AddRange(rangeSpecifier, (long) range);
        }

        public static void AddRange(this WebRequest webRequest, string rangeSpecifier, long range)
        {
            if (rangeSpecifier == null)
            {
                throw new ArgumentNullException(nameof(rangeSpecifier));
            }

            if (!AddRange(webRequest, rangeSpecifier, range.ToString(NumberFormatInfo.InvariantInfo), (range >= 0) ? "" : null))
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
                if (!string.Equals(curRange.Substring(0, curRange.IndexOf('=')), rangeSpecifier, StringComparison.OrdinalIgnoreCase))
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
        }
    }
}
