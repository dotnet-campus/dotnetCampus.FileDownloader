using System;
using System.Collections.Generic;
using System.Runtime.Serialization.Json;
using System.Text;

namespace dotnetCampus.FileDownloader
{
    class DownloadedDataManager
    {
        public DownloadedDataManager(long fileLength)
        {
            FileLength = fileLength;
        }

        private DownloadedDataManager(long fileLength, List<DataRange> downloadDataList)
        {
            FileLength = fileLength;
            DownloadDataList = downloadDataList;
        }

        /// <summary>
        /// 文档的长度
        /// </summary>
        public long FileLength { get; }

        public void AddDownloadedData(StepWriteFinishedArgs args)
        {
            lock (DownloadDataList)
            {
                var dataRange = new DataRange(args.FileStartPoint, args.DataLength);
                var i = 0;
                for (; i < DownloadDataList.Count; i++)
                {
                    // 是否落到范围内
                    var downloadedData = DownloadDataList[i];
                    if (DataRange.TryMerge(downloadedData, dataRange, out var newDataRange))
                    {
                        DownloadDataList[i] = newDataRange;
                        return;
                    }
                    else if (downloadedData.StartPoint > dataRange.StartPoint)
                    {
                        break;
                    }
                }

                if (i == DownloadDataList.Count)
                {
                    DownloadDataList.Add(dataRange);
                }
                else
                {
                    DownloadDataList.Insert(i, dataRange);
                }
            }
        }

        internal List<DownloadSegment> GetCurrentDownloadSegment()
        {
            lock (DownloadDataList)
            {
                DownloadDataList.Sort();

                long lastPoint = 0;

                if (DownloadDataList.Count == 0)
                {
                    return new List<DownloadSegment>(1) { new DownloadSegment(0, FileLength) };
                }
                else if (DownloadDataList.Count == 1)
                {
                    var dataRange = DownloadDataList[0];
                    if (dataRange.StartPoint == 0)
                    {
                        return new List<DownloadSegment>(1)
                        {
                            ToDownloadSegment(dataRange),
                            new DownloadSegment(dataRange.LastPoint, FileLength-dataRange.LastPoint)
                        };
                    }
                    else
                    {
                        var first = new DownloadSegment(0, dataRange.StartPoint - 0);
                        if (dataRange.LastPoint < FileLength)
                        {
                            var last = new DownloadSegment(dataRange.LastPoint, FileLength - dataRange.LastPoint);
                            return new List<DownloadSegment>(2)
                            {
                                first,
                                ToDownloadSegment(dataRange),
                                last
                            };
                        }
                        else
                        {
                            return new List<DownloadSegment>(1)
                            {
                                first,
                                ToDownloadSegment(dataRange),
                            };
                        }
                    }
                }
                else
                {
                    var downloadSegmentList = new List<DownloadSegment>();
                    var first = DownloadDataList[0];
                    if (first.StartPoint > 0)
                    {
                        downloadSegmentList.Add(new DownloadSegment(0, first.StartPoint - 0));
                    }
                    downloadSegmentList.Add(ToDownloadSegment(first));

                    var last = first;

                    for (var i = 1; i < DownloadDataList.Count; i++)
                    {
                        var dataRange = DownloadDataList[i];
                        lastPoint = Math.Max(lastPoint, dataRange.LastPoint);

                        var length = dataRange.StartPoint - last.LastPoint;
                        if (length > 0)
                        {
                            downloadSegmentList.Add(new DownloadSegment(last.LastPoint, length));
                        }

                        downloadSegmentList.Add(ToDownloadSegment(dataRange));
                        last = dataRange;
                    }

                    return downloadSegmentList;
                }
            }

        }

        private static DownloadSegment ToDownloadSegment(DataRange dataRange)
        {
            return new DownloadSegment(dataRange.StartPoint, dataRange.Length)
            {
                DownloadedLength = dataRange.Length
            };
        }

        public string Serialize()
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append(FileLength);
            stringBuilder.Append(BreakLine);

            foreach (var downloadData in DownloadDataList)
            {
                stringBuilder.Append(downloadData.StartPoint);
                stringBuilder.Append(',');
                stringBuilder.Append(downloadData.Length);
                stringBuilder.Append(BreakLine);
            }

            return stringBuilder.ToString();
        }

        const char BreakLine = '\n';

        public static DownloadedDataManager Deserialize(string data)
        {
            var lineList = data.Split(BreakLine);
            // 第一行的文件长度
            var fileLength = long.Parse(lineList[0]);

            var downloadDataList = new List<DataRange>();
            for (var i = 1; i < lineList.Length; i++)
            {
                var text = lineList[i];

                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                var downloadedData = text.Split(',');
                var startPoint = long.Parse(downloadedData[0]);
                var length = long.Parse(downloadedData[1]);

                downloadDataList.Add(new DataRange(startPoint, length));
            }

            return new DownloadedDataManager(fileLength, downloadDataList);
        }

        private List<DataRange> DownloadDataList { get; } = new List<DataRange>();
    }

    class DataRange : IComparer<DataRange>, IEquatable<DataRange>
    {
        public DataRange(long startPoint, long length)
        {
            StartPoint = startPoint;
            Length = length;
        }

        public long StartPoint { get; }

        public long Length { get; }

        public long LastPoint => StartPoint + Length;

        public int Compare(DataRange x, DataRange y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (ReferenceEquals(null, y))
            {
                return 1;
            }

            if (ReferenceEquals(null, x))
            {
                return -1;
            }

            return x.StartPoint.CompareTo(y.StartPoint);
        }

        public static bool TryMerge(DataRange a, DataRange b, out DataRange newDataRange)
        {
            newDataRange = null!;
            if (a.StartPoint > b.StartPoint)
            {
                var t = a;
                a = b;
                b = t;
            }

            if (a.Equals(b))
            {
                newDataRange = a;
                return true;
            }

            if (a.StartPoint <= b.StartPoint && a.LastPoint > b.StartPoint)
            {
                var lastPoint = Math.Max(a.LastPoint, b.LastPoint);
                var length = lastPoint - a.StartPoint;
                newDataRange = new DataRange(a.StartPoint, length);
                return true;
            }

            return false;
        }

        public bool Equals(DataRange? other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return StartPoint == other.StartPoint && Length == other.Length;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((DataRange) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StartPoint.GetHashCode() * 397) ^ Length.GetHashCode();
            }
        }
    }
}
