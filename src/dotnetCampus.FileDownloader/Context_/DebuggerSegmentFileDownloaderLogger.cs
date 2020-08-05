using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace dotnetCampus.FileDownloader
{
    internal class DebuggerSegmentFileDownloaderLogger : ILogger<SegmentFileDownloader>
    {
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
        {
            Debug.WriteLine(formatter(state, exception));
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return _empty;
        }

        private readonly Empty _empty = new Empty();

        private class Empty : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}