using System.Linq;
using System.Text.Json.Nodes;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Converters;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Download.Clients.QBittorrent;
using NzbDrone.Core.Download.Clients.Sabnzbd;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class ModuleDownloadClientCategoriesFixture : MigrationTest<module_download_client_categories>
    {
        // The settings as the previous schema stored them, per client: the movie-named keys, plus the
        // three clients that kept the category (and, for Freebox Download, the priorities) elsewhere.
        private const string MovieKeys = "{\"host\":\"localhost\",\"movieCategory\":\"radarr\",\"movieImportedCategory\":\"radarr-done\",\"recentMoviePriority\":3,\"olderMoviePriority\":4}";
        private const string TvCategoryKeys = "{\"host\":\"localhost\",\"tvCategory\":\"radarr\",\"recentMoviePriority\":3,\"olderMoviePriority\":4}";
        private const string CategoryOnly = "{\"host\":\"localhost\",\"tvCategory\":\"radarr\"}";
        private const string FreeboxKeys = "{\"host\":\"localhost\",\"category\":\"radarr\",\"recentPriority\":3,\"olderPriority\":4}";
        private const string HadoukenKeys = "{\"host\":\"localhost\",\"category\":\"radarr\"}";

        private static readonly string[] OldKeys =
        {
            "movieCategory", "movieImportedCategory", "recentMoviePriority", "olderMoviePriority", "tvCategory", "category", "recentPriority", "olderPriority"
        };

        private IDirectDataMapper GivenClient(string implementation, string settings)
        {
            return WithMigrationTestDb(m =>
            {
                m.Insert.IntoTable("DownloadClients").Row(new
                {
                    Enable = true,
                    Name = implementation,
                    Implementation = implementation,
                    Settings = settings,
                    ConfigContract = implementation + "Settings",
                    Priority = 1,
                    RemoveCompletedDownloads = true,
                    RemoveFailedDownloads = true
                });
            });
        }

        private static string StoredSettings(IDirectDataMapper db)
        {
            return (string)db.Query("SELECT \"Settings\" FROM \"DownloadClients\"").Single()["Settings"];
        }

        private static ModuleCategorySettings Movies<T>(string json)
            where T : IModuleCategorySettings
        {
            // Read the way the application reads a stored row.
            var settings = new EmbeddedDocumentConverter<T>().Parse(json);

            settings.ModuleCategories.Should().ContainSingle();

            return settings.ModuleCategories.Single();
        }

        // For clients that no longer exist in the code the stored row can only be read as JSON.
        private static ModuleCategorySettings MoviesEntry(string json)
        {
            var entries = JsonNode.Parse(json)["moduleCategories"].AsArray();

            entries.Should().ContainSingle();

            var entry = entries[0].AsObject();

            return new ModuleCategorySettings
            {
                Kind = entry["kind"].GetValue<string>() == "movies" ? MediaKind.Movies : MediaKind.Tv,
                Category = (string)entry["category"],
                ImportedCategory = (string)entry["importedCategory"],
                RecentPriority = (int?)entry["recentPriority"] ?? 0,
                OlderPriority = (int?)entry["olderPriority"] ?? 0
            };
        }

        [TestCase("QBittorrent", MovieKeys)]
        [TestCase("Deluge", MovieKeys)]
        [TestCase("RTorrent", MovieKeys)]
        [TestCase("UTorrent", MovieKeys)]
        [TestCase("Transmission", MovieKeys)]
        [TestCase("Vuze", MovieKeys)]
        [TestCase("Sabnzbd", MovieKeys)]
        [TestCase("Nzbget", MovieKeys)]
        [TestCase("NzbVortex", TvCategoryKeys)]
        [TestCase("TorrentDownloadStation", CategoryOnly)]
        [TestCase("UsenetDownloadStation", CategoryOnly)]
        [TestCase("TorrentFreeboxDownload", FreeboxKeys)]
        [TestCase("Hadouken", HadoukenKeys)]
        public void should_remove_the_old_keys_and_keep_the_other_settings(string implementation, string settings)
        {
            var json = StoredSettings(GivenClient(implementation, settings));

            // Top-level keys only: the entries inside the list have a category of their own.
            var keys = JsonNode.Parse(json).AsObject().Select(p => p.Key).ToList();

            keys.Should().Contain("host");
            keys.Should().Contain("moduleCategories");
            keys.Should().NotContain(OldKeys);
        }

        [TestCase("QBittorrent")]
        [TestCase("Deluge")]
        [TestCase("RTorrent")]
        [TestCase("UTorrent")]
        [TestCase("Transmission")]
        [TestCase("Vuze")]
        [TestCase("Sabnzbd")]
        [TestCase("Nzbget")]
        public void should_move_the_movie_values_to_the_movies_entry(string implementation)
        {
            var json = StoredSettings(GivenClient(implementation, MovieKeys));

            var entry = implementation switch
            {
                "QBittorrent" => Movies<QBittorrentSettings>(json),
                "Sabnzbd" => Movies<SabnzbdSettings>(json),
                _ => MoviesEntry(json)
            };

            entry.Kind.Should().Be(MediaKind.Movies);
            entry.Category.Should().Be("radarr");
            entry.ImportedCategory.Should().Be("radarr-done");
            entry.RecentPriority.Should().Be(3);
            entry.OlderPriority.Should().Be(4);
        }

        [Test]
        public void should_move_the_tv_category_of_nzbvortex()
        {
            var entry = MoviesEntry(StoredSettings(GivenClient("NzbVortex", TvCategoryKeys)));

            entry.Kind.Should().Be(MediaKind.Movies);
            entry.Category.Should().Be("radarr");
            entry.RecentPriority.Should().Be(3);
            entry.OlderPriority.Should().Be(4);
        }

        [TestCase("TorrentDownloadStation")]
        [TestCase("UsenetDownloadStation")]
        public void should_move_the_tv_category_of_download_station(string implementation)
        {
            var entry = MoviesEntry(StoredSettings(GivenClient(implementation, CategoryOnly)));

            entry.Kind.Should().Be(MediaKind.Movies);
            entry.Category.Should().Be("radarr");
        }

        [Test]
        public void should_move_the_category_and_priorities_of_freebox_download()
        {
            var entry = MoviesEntry(StoredSettings(GivenClient("TorrentFreeboxDownload", FreeboxKeys)));

            entry.Kind.Should().Be(MediaKind.Movies);
            entry.Category.Should().Be("radarr");
            entry.RecentPriority.Should().Be(3);
            entry.OlderPriority.Should().Be(4);
        }

        [Test]
        public void should_move_the_category_of_hadouken()
        {
            var entry = MoviesEntry(StoredSettings(GivenClient("Hadouken", HadoukenKeys)));

            entry.Kind.Should().Be(MediaKind.Movies);
            entry.Category.Should().Be("radarr");
        }

        [TestCase("{\"host\":\"localhost\",\"movieCategory\":\"\"}")]
        [TestCase("{\"host\":\"localhost\",\"movieCategory\":null}")]
        [TestCase("{\"host\":\"localhost\"}")]
        public void should_keep_an_empty_category_empty(string settings)
        {
            var entry = Movies<QBittorrentSettings>(StoredSettings(GivenClient("QBittorrent", settings)));

            // Not the new default category: an empty category means the client takes every download.
            entry.Category.Should().BeNullOrEmpty();
            entry.ImportedCategory.Should().BeNull();
        }

        [Test]
        public void should_read_old_keys_in_any_case()
        {
            var entry = Movies<QBittorrentSettings>(StoredSettings(GivenClient("QBittorrent", "{\"MovieCategory\":\"radarr\",\"RecentMoviePriority\":3}")));

            entry.Category.Should().Be("radarr");
            entry.RecentPriority.Should().Be(3);
        }

        [Test]
        public void should_leave_settings_that_are_already_migrated_alone()
        {
            const string settings = "{\"host\":\"localhost\",\"moduleCategories\":[{\"kind\":\"movies\",\"category\":\"kept\"},{\"kind\":\"tv\",\"category\":\"kept-tv\"}]}";

            var db = GivenClient("QBittorrent", settings);

            var parsed = new EmbeddedDocumentConverter<QBittorrentSettings>().Parse(StoredSettings(db));

            parsed.ModuleCategories.Select(c => (c.Kind, c.Category)).Should().Equal((MediaKind.Movies, "kept"), (MediaKind.Tv, "kept-tv"));
        }

        [TestCase("Aria2")]
        [TestCase("Flood")]
        [TestCase("Pneumatic")]
        [TestCase("TorrentBlackhole")]
        [TestCase("UsenetBlackhole")]
        public void should_not_touch_a_client_without_categories(string implementation)
        {
            const string settings = "{\"host\":\"localhost\",\"category\":\"kept\",\"movieCategory\":\"kept\"}";

            StoredSettings(GivenClient(implementation, settings)).Should().Be(settings);
        }

        [Test]
        public void should_migrate_every_client_in_the_table()
        {
            var db = WithMigrationTestDb(m =>
            {
                foreach (var (name, settings) in new[] { ("QBittorrent", MovieKeys), ("Sabnzbd", MovieKeys), ("Aria2", "{\"host\":\"a\"}") })
                {
                    m.Insert.IntoTable("DownloadClients").Row(new
                    {
                        Enable = true,
                        Name = name,
                        Implementation = name,
                        Settings = settings,
                        ConfigContract = name + "Settings",
                        Priority = 1,
                        RemoveCompletedDownloads = true,
                        RemoveFailedDownloads = true
                    });
                }
            });

            var stored = db.Query("SELECT \"Implementation\", \"Settings\" FROM \"DownloadClients\"")
                           .ToDictionary(r => (string)r["Implementation"], r => (string)r["Settings"]);

            stored["QBittorrent"].Should().Contain("moduleCategories");
            stored["Sabnzbd"].Should().Contain("moduleCategories");
            stored["Aria2"].Should().Be("{\"host\":\"a\"}");
        }

        [Test]
        public void should_migrate_an_empty_table()
        {
            var db = WithMigrationTestDb();

            db.Query("SELECT \"Id\" FROM \"DownloadClients\"").Should().BeEmpty();
        }
    }
}
