using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NzbDrone.Core.Datastore.Converters;

namespace NzbDrone.Core.Upstream
{
    // What every stage does with a JSON column: parse it with the same converter the target's
    // repositories read it with, so a shape the target cannot read fails here, on one row, and
    // not later on a page load. A column that is only copied (images, genres, media info) is
    // parsed from its stored text; a column that needs its IDs remapped first goes through
    // JToken and the catalog, and is parsed from the rewritten document.
    public static class SourceDocuments
    {
        // Null for an absent, NULL or blank column.
        public static T Parse<T>(SourceRow row, string column, EmbeddedDocumentConverter<T> converter)
            where T : class
        {
            var text = row.GetString(column);

            return string.IsNullOrWhiteSpace(text) ? null : converter.Parse(text);
        }

        // Null for an absent column or a JSON null.
        public static T Parse<T>(JToken document, EmbeddedDocumentConverter<T> converter)
            where T : class
        {
            return document == null || document.Type == JTokenType.Null ? null : converter.Parse(document.ToString(Formatting.None));
        }

        // A stored document the upstream app could never have written, or one the target cannot
        // read: a stage skips that row as CORRUPT. Anything else (a missing mandatory column, a
        // bug) is not a bad document and stops the run.
        public static bool IsBad(Exception ex)
        {
            return ex is CorruptSourceDataException or System.Text.Json.JsonException or TypeLoadException or InvalidCastException or FormatException or OverflowException;
        }

        // The exception type and its message name a column or a JSON path, never a stored value;
        // the report is shared.
        public static string Describe(Exception ex)
        {
            return ex is CorruptSourceDataException ? ex.Message : ex.GetType().Name;
        }
    }
}
