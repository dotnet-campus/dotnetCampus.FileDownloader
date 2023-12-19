using Microsoft.UI.Dispatching;

namespace UnoFileDownloader.Utils
{
    public record DispatcherQueueProvider(DispatcherQueue Dispatcher) : IDispatcherQueueProvider
    {
    }

    public interface IDispatcherQueueProvider
    {
        Microsoft.UI.Dispatching.DispatcherQueue Dispatcher { get; }
    }
}
