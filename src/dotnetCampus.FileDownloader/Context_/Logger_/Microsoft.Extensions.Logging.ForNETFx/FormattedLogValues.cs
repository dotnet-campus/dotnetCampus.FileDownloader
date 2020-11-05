using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace dotnetCampus.FileDownloader
{
    /// <summary>
    /// LogValues to enable formatting options supported by <see cref="M:string.Format" />.
    /// This also enables using {NamedformatItem} in the format string.
    /// </summary>
    internal readonly struct FormattedLogValues : IReadOnlyList<KeyValuePair<string, object>>, IEnumerable<KeyValuePair<string, object>>, IEnumerable, IReadOnlyCollection<KeyValuePair<string, object>>
    {
        private static ConcurrentDictionary<string, LogValuesFormatter> _formatters = new ConcurrentDictionary<string, LogValuesFormatter>();
        internal const int MaxCachedFormatters = 1024;
        private const string NullFormat = "[null]";
        private static int _count;
        private readonly LogValuesFormatter _formatter;
        private readonly object[] _values;
        private readonly string _originalMessage;

        internal LogValuesFormatter Formatter
        {
            get
            {
                return this._formatter;
            }
        }

        public FormattedLogValues(string format, params object[] values)
        {
            if (values != null && values.Length != 0 && format != null)
            {
                if (FormattedLogValues._count >= 1024)
                {
                    if (!FormattedLogValues._formatters.TryGetValue(format, out this._formatter))
                        this._formatter = new LogValuesFormatter(format);
                }
                else
                    this._formatter = FormattedLogValues._formatters.GetOrAdd(format, (Func<string, LogValuesFormatter>) (f =>
                    {
                        Interlocked.Increment(ref FormattedLogValues._count);
                        return new LogValuesFormatter(f);
                    }));
            }
            else
                this._formatter = (LogValuesFormatter) null!;
            this._originalMessage = format ?? "[null]";
            this._values = values!;
        }

        public KeyValuePair<string, object> this[int index]
        {
            get
            {
                if (index < 0 || index >= this.Count)
                    throw new IndexOutOfRangeException(nameof(index));
                return index == this.Count - 1 ? new KeyValuePair<string, object>("{OriginalFormat}", (object) this._originalMessage) : this._formatter.GetValue(this._values, index);
            }
        }

        public int Count
        {
            get
            {
                return this._formatter == null ? 1 : this._formatter.ValueNames.Count + 1;
            }
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            for (int i = 0; i < this.Count; ++i)
                yield return this[i];
        }

        public override string ToString()
        {
            return this._formatter == null ? this._originalMessage : this._formatter.Format(this._values);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (IEnumerator) this.GetEnumerator();
        }
    }
}
