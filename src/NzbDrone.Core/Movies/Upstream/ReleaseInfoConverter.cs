using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NzbDrone.Core.Datastore.Converters;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Movies.Upstream
{
    // Converts the Release column of Radarr's DownloadHistory (a serialized
    // ReleaseInfo in Radarr's shape) to the shared ReleaseInfo (docs/importer.md,
    // "The DownloadHistory.Release conversion"; plan finding 1). A wrong result does
    // not fail the import; it breaks blocklisting and re-grab decisions later, so
    // every difference below has a test.
    //
    // What differs, checked against the owner's database (141 grabbed rows) and
    // Radarr's code:
    //
    //   languages     Radarr kept Language values; the shared model keeps names as
    //                 strings. All 141 real rows hold an empty list, so the other
    //                 shapes (catalog IDs, {id, name} objects, names) are from the
    //                 code and not yet seen in a real row.
    //   indexerFlags  Not stored in any real row. If a row has it as a number, it is
    //                 Radarr's IndexerFlags value and becomes the named flag set.
    //   indexerId     A Radarr indexer ID; the caller maps it to the imported
    //                 indexer, or to 0 when none matches (211 of the owner's 352
    //                 rows already hold 0, and 81 of the other 141 are dangling).
    //
    // Everything else (title, size, guid, URLs, protocol, TMDb and IMDb IDs, publish
    // date) reads as it is. Torrent fields (seeders, peers, info hash) are dropped
    // exactly as Radarr drops them when it reads the column back.
    //
    // downloadUrl holds the Prowlarr API key. Nothing here logs a value.
    //
    // This is module code (the flag vocabulary is the movie module's), so the core importer
    // cannot reference it directly; see docs/importer.md, "Module seam".
    public static class ReleaseInfoConverter
    {
        private static readonly EmbeddedDocumentConverter<ReleaseInfo> Reader = new EmbeddedDocumentConverter<ReleaseInfo>();

        public static ReleaseInfo Convert(string releaseJson, IUpstreamCatalog catalog, Func<int, int> mapIndexerId)
        {
            JObject release;

            try
            {
                release = JToken.Parse(releaseJson) as JObject;
            }
            catch (JsonReaderException ex)
            {
                throw new CorruptSourceDataException("A stored release is not valid JSON", ex);
            }

            if (release == null)
            {
                throw new CorruptSourceDataException("A stored release is not a JSON object");
            }

            ConvertLanguages(release, catalog);

            var flags = TakeIndexerFlags(release);

            RemapIndexer(release, mapIndexerId);

            ReleaseInfo info;

            try
            {
                info = Reader.Parse(release.ToString(Formatting.None));
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw new CorruptSourceDataException("A stored release does not fit the release model", ex);
            }

            if (flags != 0)
            {
                info.AddIndexerFlags(flags);
            }

            return info;
        }

        private static void ConvertLanguages(JObject release, IUpstreamCatalog catalog)
        {
            var property = release.Property("languages", StringComparison.OrdinalIgnoreCase);

            if (property == null || property.Value.Type == JTokenType.Null)
            {
                return;
            }

            if (property.Value is not JArray array)
            {
                throw new CorruptSourceDataException("A stored release has a languages value that is not a list");
            }

            var names = new JArray();

            foreach (var item in array)
            {
                names.Add(LanguageName(item, catalog));
            }

            property.Value = names;
        }

        private static string LanguageName(JToken item, IUpstreamCatalog catalog)
        {
            switch (item.Type)
            {
                case JTokenType.Integer:
                    return NameOf(item.Value<int>(), catalog);

                case JTokenType.String:
                    return item.Value<string>();

                case JTokenType.Object:
                    var obj = (JObject)item;
                    var id = obj.GetValue("id", StringComparison.OrdinalIgnoreCase);

                    if (id != null && id.Type == JTokenType.Integer)
                    {
                        return NameOf(id.Value<int>(), catalog);
                    }

                    var name = obj.GetValue("name", StringComparison.OrdinalIgnoreCase);

                    if (name != null && name.Type == JTokenType.String)
                    {
                        return name.Value<string>();
                    }

                    break;
            }

            throw new CorruptSourceDataException("A stored release has a language this importer cannot read");
        }

        private static string NameOf(int upstreamId, IUpstreamCatalog catalog)
        {
            return Language.FindById(CatalogRemapper.LanguageId(upstreamId, catalog)).Name;
        }

        // Removed whatever its shape, because the shared model would not read a number.
        private static IndexerFlags TakeIndexerFlags(JObject release)
        {
            var property = release.Property("indexerFlags", StringComparison.OrdinalIgnoreCase);

            if (property == null)
            {
                return 0;
            }

            property.Remove();

            if (property.Value.Type == JTokenType.Null)
            {
                return 0;
            }

            if (property.Value.Type != JTokenType.Integer)
            {
                throw new CorruptSourceDataException("A stored release has an indexerFlags value that is not a number");
            }

            return (IndexerFlags)property.Value.Value<int>();
        }

        private static void RemapIndexer(JObject release, Func<int, int> mapIndexerId)
        {
            var property = release.Property("indexerId", StringComparison.OrdinalIgnoreCase);

            if (property == null || property.Value.Type == JTokenType.Null)
            {
                return;
            }

            if (property.Value.Type != JTokenType.Integer)
            {
                throw new CorruptSourceDataException("A stored release has an indexerId that is not a number");
            }

            property.Value = mapIndexerId(property.Value.Value<int>());
        }
    }
}
