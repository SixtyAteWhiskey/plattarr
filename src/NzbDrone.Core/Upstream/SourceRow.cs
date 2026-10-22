using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace NzbDrone.Core.Upstream
{
    // One row of an upstream table, read without interpretation. Columns are
    // matched case-insensitively and may be absent: an older upstream schema has
    // fewer columns, and the adapter reads the columns it finds.
    //
    // Reading rules: GetString, GetNullableInt, GetNullableLong, GetNullableDouble and GetJson return
    // null for an absent or NULL column. GetInt, GetLong and GetBool throw for
    // both, so a mandatory column that is missing is loud. Exception messages name
    // the column and never the value (rows hold secrets).
    public class SourceRow
    {
        private readonly Dictionary<string, object> _values;

        public SourceRow(IDictionary<string, object> values)
        {
            _values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in values)
            {
                _values[pair.Key] = pair.Value is DBNull ? null : pair.Value;
            }
        }

        public IEnumerable<string> Columns => _values.Keys;

        public bool Has(string column)
        {
            return _values.ContainsKey(column);
        }

        public bool IsNull(string column)
        {
            return !_values.TryGetValue(column, out var value) || value == null;
        }

        public string GetString(string column)
        {
            if (IsNull(column))
            {
                return null;
            }

            var value = _values[column];

            return value is byte[] bytes
                ? System.Text.Encoding.UTF8.GetString(bytes)
                : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public long? GetNullableLong(string column)
        {
            if (IsNull(column))
            {
                return null;
            }

            try
            {
                return Convert.ToInt64(_values[column], CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                throw new UpstreamImportException($"Column {column} does not hold an integer", ex);
            }
        }

        public double? GetNullableDouble(string column)
        {
            if (IsNull(column))
            {
                return null;
            }

            try
            {
                return Convert.ToDouble(_values[column], CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                throw new UpstreamImportException($"Column {column} does not hold a number", ex);
            }
        }

        public long GetLong(string column)
        {
            return GetNullableLong(column) ?? throw MissingValue(column);
        }

        public int? GetNullableInt(string column)
        {
            var value = GetNullableLong(column);

            if (value == null)
            {
                return null;
            }

            if (value < int.MinValue || value > int.MaxValue)
            {
                throw new UpstreamImportException($"Column {column} does not fit in 32 bits");
            }

            return (int)value;
        }

        public int GetInt(string column)
        {
            return GetNullableInt(column) ?? throw MissingValue(column);
        }

        // SQLite stores booleans as 0 and 1.
        public bool GetBool(string column)
        {
            return GetLong(column) != 0;
        }

        // The stored JSON text, parsed. Null for an absent, NULL or empty column.
        public JToken GetJson(string column)
        {
            var text = GetString(column);

            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            try
            {
                return JToken.Parse(text);
            }
            catch (Exception ex) when (ex is Newtonsoft.Json.JsonReaderException)
            {
                throw new UpstreamImportException($"Column {column} does not hold valid JSON", ex);
            }
        }

        private static UpstreamImportException MissingValue(string column)
        {
            return new UpstreamImportException($"Column {column} is missing or NULL");
        }
    }
}
