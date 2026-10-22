using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    // docs/importer.md, "Download clients": a client imported from Radarr and a client
    // migrated in place by migration 003 must end up with the same settings. Each row is
    // given to migration 003 as the previous schema stored it, and to the importer's
    // converter, and the two results are compared as text.
    [TestFixture]
    public class UpstreamClientSettingsMigrationFixture : MigrationTest<module_download_client_categories>
    {
        private const string QBittorrent =
            "{\"host\":\"qbit.invalid\",\"port\":8080,\"useSsl\":false,\"username\":\"user\",\"password\":\"FAKE-PASSWORD\","
            + "\"movieCategory\":\"radarr\",\"recentMoviePriority\":0,\"olderMoviePriority\":0,\"initialState\":0,"
            + "\"sequentialOrder\":false,\"firstAndLast\":false,\"contentLayout\":0}";

        private const string QBittorrentFull =
            "{\"host\":\"qbit.invalid\",\"movieCategory\":\"radarr\",\"movieImportedCategory\":\"radarr-done\",\"recentMoviePriority\":3,\"olderMoviePriority\":4}";

        private const string QBittorrentEmptyCategory = "{\"host\":\"qbit.invalid\",\"movieCategory\":\"\"}";
        private const string QBittorrentUpperCase = "{\"host\":\"qbit.invalid\",\"MovieCategory\":\"radarr\",\"RecentMoviePriority\":1}";
        private const string QBittorrentNothingStored = "{\"host\":\"qbit.invalid\",\"port\":8080}";
        private const string QBittorrentAlreadyNewShape = "{\"host\":\"qbit.invalid\",\"movieCategory\":\"stale\",\"moduleCategories\":[{\"kind\":\"movies\",\"category\":\"kept\"}]}";
        private const string Sabnzbd = "{\"host\":\"sab.invalid\",\"apiKey\":\"FAKEAPIKEY0000\",\"movieCategory\":\"movies\",\"recentMoviePriority\":-100,\"olderMoviePriority\":-100}";

        [TestCase("QBittorrent", QBittorrent)]
        [TestCase("QBittorrent", QBittorrentFull)]
        [TestCase("QBittorrent", QBittorrentEmptyCategory)]
        [TestCase("QBittorrent", QBittorrentUpperCase)]
        [TestCase("QBittorrent", QBittorrentNothingStored)]
        [TestCase("QBittorrent", QBittorrentAlreadyNewShape)]
        [TestCase("Sabnzbd", Sabnzbd)]
        public void importer_should_produce_what_the_migration_produces(string implementation, string oldSettings)
        {
            var db = WithMigrationTestDb(m =>
            {
                m.Insert.IntoTable("DownloadClients").Row(new
                {
                    Enable = true,
                    Name = implementation,
                    Implementation = implementation,
                    Settings = oldSettings,
                    ConfigContract = implementation + "Settings",
                    Priority = 1,
                    RemoveCompletedDownloads = true,
                    RemoveFailedDownloads = true
                });
            });

            var migrated = (string)db.Query("SELECT \"Settings\" FROM \"DownloadClients\"").Single()["Settings"];

            DownloadClientSettingsConverter.Convert(implementation, oldSettings).Should().Be(migrated);
        }
    }
}
