using System.Threading.Tasks;

namespace dotnetCampus.FileDownloader
{
    /// <summary>
    /// 表示支持异步的释放，这是兼容的代码，用于支持非 .NET Core 的框架
    /// </summary>
    public interface IAsyncDisposable
    {
        /// <summary>
        /// 异步释放对象
        /// </summary>
        /// <returns></returns>
        Task DisposeAsync();
    }
}
