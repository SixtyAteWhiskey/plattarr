using System.Collections.Generic;
using System.IO;
using System.Linq;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Upstream
{
    // Scrubbed rows in the shapes found in the owner's databases
    // (docs/importer.md, "Dump findings"), inserted into a database built from the
    // real schema, so a test reads them back through the adapter with the true
    // column types. Hosts are *.invalid, keys and passwords are obvious fakes, titles
    // are Sintel (CC-BY, Blender Foundation). IDs and JSON structure are as found.
    //
    // These rows are written by hand from the documented shapes. They are replaced
    // by rows extracted from a scrubbed dump when tools/upstream-fixtures exists;
    // until then, a shape this file gets wrong is a shape the next real dump will show.
    public abstract class UpstreamFixtureTest : CoreTest
    {
        public const string FakeApiKey = "FAKEAPIKEY0000";

        // A grab row's Release as the database holds it: camelCase ReleaseInfo, integer imdbId,
        // empty languages, the Prowlarr API key inside downloadUrl, no indexerFlags.
        protected const string GrabbedRelease =
            "{\"guid\":\"https://tracker.invalid/details/1001\",\"title\":\"Sintel.2010.1080p.WEB-DL.x264-BLENDER\","
            + "\"size\":3221225472,\"downloadUrl\":\"http://prowlarr.invalid:9696/13/download?apikey=" + FakeApiKey + "&link=ABC&file=Sintel\","
            + "\"infoUrl\":\"https://tracker.invalid/details/1001\",\"commentUrl\":\"https://tracker.invalid/details/1001#comments\","
            + "\"magnetUrl\":\"magnet:?xt=urn:btih:0123456789abcdef0123456789abcdef01234567\",\"infoHash\":\"0123456789abcdef0123456789abcdef01234567\","
            + "\"seeders\":12,\"peers\":14,\"indexerId\":1,\"indexer\":\"Tracker One (Prowlarr)\",\"indexerPriority\":25,"
            + "\"downloadProtocol\":\"torrent\",\"tmdbId\":45745,\"imdbId\":1727587,\"publishDate\":\"2026-01-02T03:04:05Z\","
            + "\"languages\":[],\"age\":3,\"ageHours\":72.5,\"ageMinutes\":4350.0}";

        private const string QBittorrentSettings =
            "{\"host\":\"qbit.invalid\",\"port\":8080,\"useSsl\":false,\"username\":\"user\",\"password\":\"FAKE-PASSWORD\","
            + "\"movieCategory\":\"radarr\",\"recentMoviePriority\":0,\"olderMoviePriority\":0,\"initialState\":0,"
            + "\"sequentialOrder\":false,\"firstAndLast\":false,\"contentLayout\":0}";

        private const string GrabData =
            "{\"downloadClient\":\"QBittorrent\",\"downloadClientName\":\"qBittorrent\",\"indexer\":\"Tracker One (Prowlarr)\",\"customFormatScore\":\"0\"}";

        // The Radarr side of one small library. Radarr indexer 1 and 2 were synchronized
        // from Prowlarr indexers 13 and 37; 3 points at a Prowlarr indexer that only
        // Sonarr received; 4 points at an indexer Prowlarr does not have; 5 was added by hand.
        protected static readonly string[] RadarrRows =
        {
            Insert("Tags", "Id, Label", "1, 'Kids'"),

            Insert(
                "DownloadClients",
                "Id, Enable, Name, Implementation, Settings, ConfigContract, Priority, RemoveCompletedDownloads, RemoveFailedDownloads, Tags",
                "1, 1, 'qBittorrent', 'QBittorrent', " + Sql(QBittorrentSettings) + ", 'QBittorrentSettings', 1, 1, 1, '[1]'"),

            TorznabIndexer(1, "Tracker One (Prowlarr)", "http://prowlarr.invalid:9696/13/"),
            TorznabIndexer(2, "Open Tracker (Prowlarr)", "http://prowlarr.invalid:9696/37/"),
            TorznabIndexer(3, "Coincidence (Prowlarr)", "http://prowlarr.invalid:9696/41/"),
            TorznabIndexer(4, "Gone (Prowlarr)", "http://prowlarr.invalid:9696/99/"),
            TorznabIndexer(5, "Hand Added", "http://tracker.invalid/"),

            Insert(
                "DownloadHistory",
                "Id, EventType, MovieId, DownloadId, SourceTitle, Date, Protocol, IndexerId, DownloadClientId, Release, Data",
                "1, 1, 1, '0123456789ABCDEF0123456789ABCDEF01234567', 'Sintel.2010.1080p.WEB-DL.x264-BLENDER', '2026-01-02 03:04:05', 2, 1, 1, " + Sql(GrabbedRelease) + ", " + Sql(GrabData))
        };

        // The Prowlarr side. Indexer 13 is Cardigann, 37 is native, 41 was synchronized to
        // Sonarr only. Applications are Radarr (1), Sonarr (2), LazyLibrarian (3), Lidarr (4);
        // the sync records pair Prowlarr 13 and 37 with Radarr's indexers 1 and 2, and Prowlarr
        // 13 and 41 with Sonarr's. Sonarr's indexer 1 is not Radarr's indexer 1.
        protected static readonly string[] ProwlarrRows =
        {
            Insert("Tags", "Id, Label", "1, 'flaresolverr'"),
            Insert("Tags", "Id, Label", "2, 'kids'"),
            Insert("AppSyncProfiles", "Id, Name, EnableRss, EnableInteractiveSearch, EnableAutomaticSearch, MinimumSeeders", "1, 'Standard', 1, 1, 1, 1"),

            ProwlarrIndexer(13, "Tracker One", "Cardigann", "CardigannSettings"),
            ProwlarrIndexer(37, "Open Tracker", "OpenTracker", "NoAuthTorrentBaseSettings"),
            ProwlarrIndexer(41, "Sonarr Only", "Cardigann", "CardigannSettings"),

            Application(1, "Radarr", "Radarr"),
            Application(2, "Sonarr", "Sonarr"),
            Application(3, "LazyLibrarian", "LazyLibrarian"),
            Application(4, "Lidarr", "Lidarr"),

            Mapping(1, 13, 1, 1),
            Mapping(2, 37, 1, 2),
            Mapping(3, 13, 2, 1),
            Mapping(4, 41, 2, 3)
        };

        protected SourceSnapshot ReadRadarr(params string[] extraStatements)
        {
            return Read(new RadarrAdapter(), "radarr-242.schema.sql", 242, RadarrRows.Concat(extraStatements));
        }

        protected SourceSnapshot ReadProwlarr(params string[] extraStatements)
        {
            return Read(new ProwlarrAdapter(), "prowlarr-44.schema.sql", 44, ProwlarrRows.Concat(extraStatements));
        }

        // Only the rows given, so a test can leave out the base rows it does not want.
        protected SourceSnapshot ReadProwlarrOnly(params string[] statements)
        {
            return Read(new ProwlarrAdapter(), "prowlarr-44.schema.sql", 44, statements);
        }

        protected static SourceRow Row(params (string Column, object Value)[] values)
        {
            return new SourceRow(values.ToDictionary(v => v.Column, v => v.Value));
        }

        protected static string Insert(string table, string columns, string values)
        {
            return $"INSERT INTO \"{table}\" ({columns}) VALUES ({values})";
        }

        protected static string Sql(string text)
        {
            return "'" + text.Replace("'", "''") + "'";
        }

        protected static string TorznabIndexer(int id, string name, string baseUrl)
        {
            var settings = "{\"baseUrl\":\"" + baseUrl + "\",\"apiPath\":\"/api\",\"apiKey\":\"" + FakeApiKey + "\",\"minimumSeeders\":1,"
                + "\"seedCriteria\":{},\"rejectBlocklistedTorrentHashesWhileGrabbing\":false,\"requiredFlags\":[],"
                + "\"categories\":[2000,2010],\"multiLanguages\":[],\"failDownloads\":[],\"removeYear\":false}";

            return Insert(
                "Indexers",
                "Id, Name, Implementation, Settings, ConfigContract, EnableRss, EnableAutomaticSearch, EnableInteractiveSearch, Priority, Tags, DownloadClientId",
                $"{id}, {Sql(name)}, 'Torznab', {Sql(settings)}, 'TorznabSettings', 1, 1, 1, 25, '[]', 0");
        }

        protected static string ProwlarrIndexer(int id, string name, string implementation, string contract)
        {
            var settings = "{\"definitionFile\":\"tracker\",\"baseSettings\":{\"limitsUnit\":0},\"torrentBaseSettings\":{\"preferMagnetUrl\":false}}";

            return Insert(
                "Indexers",
                "Id, Name, Implementation, Settings, ConfigContract, Enable, Priority, Added, Redirect, AppProfileId, Tags, DownloadClientId",
                $"{id}, {Sql(name)}, {Sql(implementation)}, {Sql(settings)}, {Sql(contract)}, 1, 25, '2026-01-01 00:00:00', 0, 1, '[]', 0");
        }

        protected static string Application(int id, string name, string implementation)
        {
            var settings = "{\"prowlarrUrl\":\"http://prowlarr.invalid:9696\",\"baseUrl\":\"http://" + name.ToLowerInvariant() + ".invalid\",\"apiKey\":\"" + FakeApiKey + "\"}";

            return Insert(
                "Applications",
                "Id, Name, Implementation, Settings, ConfigContract, SyncLevel, Tags",
                $"{id}, {Sql(name)}, {Sql(implementation)}, {Sql(settings)}, {Sql(implementation + "Settings")}, 2, '[]'");
        }

        protected static string Mapping(int id, int indexerId, int appId, int remoteIndexerId)
        {
            return Insert(
                "ApplicationIndexerMapping",
                "Id, IndexerId, AppId, RemoteIndexerId",
                $"{id}, {indexerId}, {appId}, {remoteIndexerId}");
        }

        private SourceSnapshot Read(IUpstreamAdapter adapter, string schemaFile, long version, IEnumerable<string> rows)
        {
            var scratch = Path.Combine(TempFolder, "scratch");
            Directory.CreateDirectory(scratch);

            var source = Path.Combine(TempFolder, adapter.DatabaseFileName);

            UpstreamTestDatabase.CreateFromSchema(
                source,
                GetTestPath("Files/Upstream/" + schemaFile),
                new[] { $"INSERT INTO VersionInfo (Version, AppliedOn, Description) VALUES ({version}, '2026-10-21 00:00:00', 'test')" }
                    .Concat(rows).ToArray());

            using var copy = UpstreamSourceCopy.Create(source, adapter.DatabaseFileName, scratch);
            using var connection = copy.OpenReadOnly();

            return adapter.Read(connection, new ImportOptions());
        }
    }
}
