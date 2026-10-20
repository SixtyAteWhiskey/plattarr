using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Languages
{
    // The shared release model carries languages as the indexer reported them
    // (Prowlarr's strings). The movie pipeline works with Language. Names
    // written by Plattarr itself round-trip by Language.Name; names from an
    // indexer feed fall back to the ISO English name, as Radarr's Newznab and
    // Torznab parsers did.
    public static class IndexerLanguageMapper
    {
        private static readonly Dictionary<string, Language> ByName =
            Language.All.ToDictionary(l => l.Name, StringComparer.InvariantCultureIgnoreCase);

        public static Language Map(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var trimmed = name.Trim();

            if (ByName.TryGetValue(trimmed, out var language))
            {
                return language;
            }

            return IsoLanguages.FindByName(trimmed)?.Language;
        }

        public static List<Language> Map(IEnumerable<string> names)
        {
            if (names == null)
            {
                return new List<Language>();
            }

            return names.Select(Map)
                .Where(l => l != null)
                .Distinct()
                .ToList();
        }

        public static List<string> ToNames(IEnumerable<Language> languages)
        {
            if (languages == null)
            {
                return new List<string>();
            }

            return languages.Where(l => l != null).Select(l => l.Name).ToList();
        }
    }
}
