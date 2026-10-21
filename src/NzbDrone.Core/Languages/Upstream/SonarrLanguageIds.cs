using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Languages.Upstream
{
    // Two-way map between one upstream app's language IDs and Plattarr's catalog
    // IDs (docs/catalogs.md). Used by the migration importer and the
    // compatibility APIs. Upstream IDs come from Sonarr develop 7ccafd3.
    public static class SonarrLanguageIds
    {
        private static readonly Dictionary<int, int> ToCatalogMap = new()
        {
            { -2, Language.Original.Id },
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
            { 26, Language.Arabic.Id },
            { 27, Language.Hindi.Id },
            { 28, Language.Bulgarian.Id },
            { 29, Language.Malayalam.Id },
            { 30, Language.Ukrainian.Id },
            { 31, Language.Slovak.Id },
            { 32, Language.Thai.Id },
            { 33, Language.PortugueseBR.Id },
            { 34, Language.SpanishLatino.Id },
            { 35, Language.Romanian.Id },
            { 36, Language.Latvian.Id },
            { 37, Language.Persian.Id },
            { 38, Language.Catalan.Id },
            { 39, Language.Croatian.Id },
            { 40, Language.Serbian.Id },
            { 41, Language.Bosnian.Id },
            { 42, Language.Estonian.Id },
            { 43, Language.Tamil.Id },
            { 44, Language.Indonesian.Id },
            { 45, Language.Macedonian.Id },
            { 46, Language.Slovenian.Id },
            { 47, Language.Azerbaijani.Id },
            { 48, Language.Uzbek.Id },
            { 49, Language.Malay.Id },
            { 50, Language.Urdu.Id },
            { 51, Language.Romansh.Id },
            { 52, Language.Georgian.Id },
            { 53, Language.Irish.Id },
        };

        private static readonly Dictionary<int, int> ToUpstreamMap = ToCatalogMap.ToDictionary(p => p.Value, p => p.Key);

        public static IReadOnlyDictionary<int, int> UpstreamToCatalog => ToCatalogMap;

        // Throws for an ID upstream never had: stored data with one is corrupt.
        public static int ToCatalog(int upstreamId)
        {
            if (!ToCatalogMap.TryGetValue(upstreamId, out var catalogId))
            {
                throw new KeyNotFoundException($"Sonarr has no language with ID {upstreamId}");
            }

            return catalogId;
        }

        // False when Sonarr has no equivalent (for example a catalog entry only
        // another app parses). Callers leave the item out.
        public static bool TryToUpstream(int catalogId, out int upstreamId)
        {
            return ToUpstreamMap.TryGetValue(catalogId, out upstreamId);
        }
    }
}
