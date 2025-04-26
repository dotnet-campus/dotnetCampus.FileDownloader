﻿using System;
using System.Collections.Generic;

namespace dotnetCampus.FileDownloader.Utils.BreakpointResumptionTransmissions;

readonly struct DataRange : IComparer<DataRange>, IEquatable<DataRange>
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
        // 由于 DataRange 从引用类型修改为值类型，这就导致原本调用 ReferenceEquals 的代码为傻逼代码，注释掉，避免无用的装箱判断不相等
        //if (ReferenceEquals(x, y))
        //{
        //    return 0;
        //}

        //if (ReferenceEquals(null, y))
        //{
        //    return 1;
        //}

        //if (ReferenceEquals(null, x))
        //{
        //    return -1;
        //}

        return x.StartPoint.CompareTo(y.StartPoint);
    }

    public static bool TryMerge(DataRange a, DataRange b, out DataRange newDataRange)
    {
        newDataRange = default;
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

        if (a.StartPoint <= b.StartPoint && a.LastPoint >= b.StartPoint)
        {
            var lastPoint = Math.Max(a.LastPoint, b.LastPoint);
            var length = lastPoint - a.StartPoint;
            newDataRange = new DataRange(a.StartPoint, length);
            return true;
        }

        return false;
    }

    public bool Equals(DataRange other)
    {
        // 由于 DataRange 从引用类型修改为值类型，这就导致原本调用 ReferenceEquals 的代码为傻逼代码，注释掉，避免无用的装箱判断不相等
        //if (ReferenceEquals(null, other))
        //{
        //    return false;
        //}

        //if (ReferenceEquals(this, other))
        //{
        //    return true;
        //}

        return StartPoint == other.StartPoint && Length == other.Length;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals((DataRange) obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
#if NETCOREAPP3_1_OR_GREATER
            return HashCode.Combine(StartPoint, Length);
#else
            return (StartPoint.GetHashCode() * 397) ^ Length.GetHashCode();
#endif
        }
    }

    public override string ToString() => $"Start={StartPoint};Length={Length}";
}
