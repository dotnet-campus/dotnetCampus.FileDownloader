﻿using System;
using System.Diagnostics;

namespace dotnetCampus.FileDownloader
{
    static class FileSizeFormatter
    {
        /// <summary>
        /// 获取字节数的格式化字符串
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="formatString"></param>
        /// <returns></returns>
        public static string FormatSize(long bytes, string formatString = "{0:0.00}{1}")
            => FormatSize((double) bytes, formatString);

        /// <summary>
        /// 获取字节数的格式化字符串。因为可能存在“平均字节数”，所以支持小数
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="formatString"></param>
        /// <returns></returns>
        public static string FormatSize(double bytes, string formatString = "{0:0.00}{1}")
        {
            int counter = 0;
            double number = bytes;

            // 最大单位就是 PB 了，而 PB 是第 5 级，从 0 开始数
            // "Bytes", "KB", "MB", "GB", "TB", "PB"
            const int maxCount = 5;

            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;

                if (counter == maxCount)
                {
                    break;
                }
            }

            // long 最大长度是 8192PB
            Debug.Assert(counter <= maxCount);

            var unit = counter switch
            {
                0 => "B",
                1 => "KB",
                2 => "MB",
                3 => "GB",
                4 => "TB",
                5 => "PB",
                // 通过 maxCount 限制了最大的值就是 5 了
                _ => throw new ArgumentException("骚年，你是不是忘了更新 maxCount 等级了")
            };

            return $"{string.Format(formatString, number, unit)}";
        }
    }
}
