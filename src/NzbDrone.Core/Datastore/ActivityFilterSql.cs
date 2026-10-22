using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Datastore
{
    // WHERE fragments for the History page filters. Every media module's history repository
    // builds the same two filters, so they live here once. The table name comes from the table
    // mapper and the ids are integers, so nothing here comes from user input.
    public static class ActivityFilterSql
    {
        // Languages is stored as a JSON list of ids. The id may be the last item, a middle item,
        // sit before a CR or LF (files written on another OS), or be the only item (the old
        // single-language form). SQLite builds a character with CHAR(n); PostgreSQL has no such
        // function and takes CHR(n).
        public static string LanguageClause(DatabaseType databaseType, string table, IEnumerable<int> languages)
        {
            var chr = databaseType == DatabaseType.PostgreSQL ? "CHR" : "CHAR";
            var clauses = new List<string>();

            foreach (var language in languages)
            {
                clauses.Add($"\"{table}\".\"Languages\" LIKE '[% {language},%]'");
                clauses.Add($"\"{table}\".\"Languages\" LIKE '[% {language}' || {chr}(13) || '%]'");
                clauses.Add($"\"{table}\".\"Languages\" LIKE '[% {language}' || {chr}(10) || '%]'");
                clauses.Add($"\"{table}\".\"Languages\" LIKE '[{language}]'");
            }

            return $"({string.Join(" OR ", clauses)})";
        }

        public static string QualityClause(string table, IEnumerable<int> qualities)
        {
            return $"({string.Join(" OR ", qualities.Select(quality => $"\"{table}\".\"Quality\" LIKE '%_quality_: {quality},%'"))})";
        }
    }
}
