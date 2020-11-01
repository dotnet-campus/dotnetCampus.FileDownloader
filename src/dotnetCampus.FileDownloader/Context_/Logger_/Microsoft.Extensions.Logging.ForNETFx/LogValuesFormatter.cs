using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace dotnetCampus.FileDownloader
{
    /// <summary>
    /// Formatter to convert the named format items like {NamedformatItem} to <see cref="M:string.Format" /> format.
    /// </summary>
    internal class LogValuesFormatter
    {
        private static readonly object[] EmptyArray = new object[0];
        private static readonly char[] FormatDelimiters = new char[2]
        {
            ',',
            ':'
        };
        private readonly List<string> _valueNames = new List<string>();
        private const string NullValue = "(null)";
        private readonly string _format;

        public LogValuesFormatter(string format)
        {
            this.OriginalFormat = format;
            StringBuilder stringBuilder = new StringBuilder();
            int startIndex = 0;
            int length = format.Length;
            while (startIndex < length)
            {
                int braceIndex1 = LogValuesFormatter.FindBraceIndex(format, '{', startIndex, length);
                int braceIndex2 = LogValuesFormatter.FindBraceIndex(format, '}', braceIndex1, length);
                if (braceIndex2 == length)
                {
                    stringBuilder.Append(format, startIndex, length - startIndex);
                    startIndex = length;
                }
                else
                {
                    int indexOfAny = LogValuesFormatter.FindIndexOfAny(format, LogValuesFormatter.FormatDelimiters, braceIndex1, braceIndex2);
                    stringBuilder.Append(format, startIndex, braceIndex1 - startIndex + 1);
                    stringBuilder.Append(this._valueNames.Count.ToString((IFormatProvider) CultureInfo.InvariantCulture));
                    this._valueNames.Add(format.Substring(braceIndex1 + 1, indexOfAny - braceIndex1 - 1));
                    stringBuilder.Append(format, indexOfAny, braceIndex2 - indexOfAny + 1);
                    startIndex = braceIndex2 + 1;
                }
            }
            this._format = stringBuilder.ToString();
        }

        public string OriginalFormat { get; private set; }

        public List<string> ValueNames
        {
            get
            {
                return this._valueNames;
            }
        }

        private static int FindBraceIndex(string format, char brace, int startIndex, int endIndex)
        {
            int num1 = endIndex;
            int index = startIndex;
            int num2 = 0;
            for (; index < endIndex; ++index)
            {
                if (num2 > 0 && (int) format[index] != (int) brace)
                {
                    if (num2 % 2 == 0)
                    {
                        num2 = 0;
                        num1 = endIndex;
                    }
                    else
                        break;
                }
                else if ((int) format[index] == (int) brace)
                {
                    if (brace == '}')
                    {
                        if (num2 == 0)
                            num1 = index;
                    }
                    else
                        num1 = index;
                    ++num2;
                }
            }
            return num1;
        }

        private static int FindIndexOfAny(string format, char[] chars, int startIndex, int endIndex)
        {
            int num = format.IndexOfAny(chars, startIndex, endIndex - startIndex);
            return num != -1 ? num : endIndex;
        }

        public string Format(object[] values)
        {
            if (values != null)
            {
                for (int index = 0; index < values.Length; ++index)
                    values[index] = this.FormatArgument(values[index]);
            }
            return string.Format((IFormatProvider) CultureInfo.InvariantCulture, this._format, values ?? LogValuesFormatter.EmptyArray);
        }

        internal string Format()
        {
            return this._format;
        }

        internal string Format(object arg0)
        {
            return string.Format((IFormatProvider) CultureInfo.InvariantCulture, this._format, this.FormatArgument(arg0));
        }

        internal string Format(object arg0, object arg1)
        {
            return string.Format((IFormatProvider) CultureInfo.InvariantCulture, this._format, this.FormatArgument(arg0), this.FormatArgument(arg1));
        }

        internal string Format(object arg0, object arg1, object arg2)
        {
            return string.Format((IFormatProvider) CultureInfo.InvariantCulture, this._format, this.FormatArgument(arg0), this.FormatArgument(arg1), this.FormatArgument(arg2));
        }

        public KeyValuePair<string, object> GetValue(object[] values, int index)
        {
            if (index < 0 || index > this._valueNames.Count)
                throw new IndexOutOfRangeException(nameof(index));
            return this._valueNames.Count > index ? new KeyValuePair<string, object>(this._valueNames[index], values[index]) : new KeyValuePair<string, object>("{OriginalFormat}", (object) this.OriginalFormat);
        }

        public IEnumerable<KeyValuePair<string, object>> GetValues(
            object[] values)
        {
            KeyValuePair<string, object>[] keyValuePairArray = new KeyValuePair<string, object>[values.Length + 1];
            for (int index = 0; index != this._valueNames.Count; ++index)
                keyValuePairArray[index] = new KeyValuePair<string, object>(this._valueNames[index], values[index]);
            keyValuePairArray[keyValuePairArray.Length - 1] = new KeyValuePair<string, object>("{OriginalFormat}", (object) this.OriginalFormat);
            return (IEnumerable<KeyValuePair<string, object>>) keyValuePairArray;
        }

        private object FormatArgument(object value)
        {
            switch (value)
            {
                case null:
                    return (object) "(null)";
                case string _:
                    return value;
                case IEnumerable source:
                    return (object) string.Join<object>(", ", source.Cast<object>().Select<object, object>((Func<object, object>) (o => o ?? (object) "(null)")));
                default:
                    return value;
            }
        }
    }
}