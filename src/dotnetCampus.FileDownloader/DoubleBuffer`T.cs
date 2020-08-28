using System.Collections.Generic;

namespace dotnetCampus.FileDownloader
{
    /// <summary>
    /// 提供双缓存 线程安全列表
    /// </summary>
    /// 写入的时候写入到一个列表，通过 SwitchBuffer 方法，可以切换当前缓存
    class DoubleBuffer<T> : DoubleBuffer<List<T>, T>
    {
        public DoubleBuffer() : base(new List<T>(), new List<T>())
        {
        }
    }
}