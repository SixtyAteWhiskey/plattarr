using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Languages.Upstream
{
    // Two-way map between one upstream app's language IDs and Plattarr's catalog
    // IDs (docs/catalogs.md). Used by the migration importer and the
    // compatibility APIs. Upstream IDs come from Radarr develop 14afe52.
    public static class RadarrLanguageIds
    {
        private static readonly Dictionary<int, int> ToCatalogMap = new()
        {
            { -2, Language.Original.Id },
            { -1, Language.Any.Id },
            { 0, Language.Unknown.Id },
            { 1, Language.English.Id },
            { 2, Language.French.Id },
            { 3, Language.Spanish.Id },
            { 4, Language.German.Id },
            { 5, Language.Italian.Id },
            { 6, Language.Danish.Id },
            { 7, Language.Dutch.Id },
            { 8, Language.Japanese.Id },
            { 9, Language.Icelandic.Id },
            { 10, Language.Chinese.Id },
            { 11, Language.Russian.Id },
            { 12, Language.Polish.Id },
            { 13, Language.Vietnamese.Id },
            { 14, Language.Swedish.Id },
            { 15, Language.Norwegian.Id },
            { 16, Language.Finnish.Id },
            { 17, Language.Turkish.Id },
            { 18, Language.Portuguese.Id },
            { 19, Language.Flemish.Id },
            { 20, Language.Greek.Id },
            { 21, Language.Korean.Id },
            { 22, Language.Hungarian.Id },
            { 23, Language.Hebrew.Id },
            { 24, Language.Lithuanian.Id },
            { 25, Language.Czech.Id },
            { 26, Language.Hindi.Id },
            { 27, Language.Romanian.Id },
            { 28, Language.Thai.Id },
            { 29, Language.Bulgarian.Id },
            { 30, Language.PortugueseBR.Id },
            { 31, Language.Arabic.Id },
            { 32, Language.Ukrainian.Id },
            { 33, Language.Persian.Id },
            { 34, Language.Bengali.Id },
            { 35, Language.Slovak.Id },
            { 36, Language.Latvian.Id },
            { 37, Language.SpanishLatino.Id },
            { 38, Language.Catalan.Id },
            { 39, Language.Croatian.Id },
            { 40, Language.Serbian.Id },
            { 41, Language.Bosnian.Id },
            { 42, Language.Estonian.Id },
            { 43, Language.Tamil.Id },
            { 44, Language.Indonesian.Id },
            { 45, Language.Telugu.Id },
            { 46, Language.Macedonian.Id },
            { 47, Language.Slovenian.Id },
            { 48, Language.Malayalam.Id },
            { 49, Language.Kannada.Id },
            { 50, Language.Albanian.Id },
            { 51, Language.Afrikaans.Id },
            { 52, Language.Marathi.Id },
            { 53, Language.Tagalog.Id },
            { 54, Language.Urdu.Id },
            { 55, Language.Romansh.Id },
            { 56, Language.Mongolian.Id },
            { 57, Language.Georgian.Id },
        };

        private static readonly Dictionary<int, int> ToUpstreamMap = ToCatalogMap.ToDictionary(p => p.Value, p => p.Key);

        public static IReadOnlyDictionary<int, int> UpstreamToCatalog => ToCatalogMap;

        // Throws for an ID upstream never had: stored data with one is corrupt.
        public static int ToCatalog(int upstreamId)
        {
            if (!ToCatalogMap.TryGetValue(upstreamId, out var catalogId))
            {
                throw new KeyNotFoundException($"Radarr has no language with ID {upstreamId}");
            }

            return catalogId;
        }

        // False when Radarr has no equivalent (for example a catalog entry only
        // another app parses). Callers leave the item out.
        public static bool TryToUpstream(int catalogId, out int upstreamId)
        {
            return ToUpstreamMap.TryGetValue(catalogId, out upstreamId);
        }
    }
}
