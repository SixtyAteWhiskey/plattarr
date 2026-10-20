using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.IndexerHistory;
using NzbDrone.Core.IndexerProxies;
using NzbDrone.Core.IndexerProxies.Http;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Newznab;
using NzbDrone.Core.Indexers.Torznab;
using NzbDrone.Core.Test.Framework;
using LegacyIndexerDefinition = NzbDrone.Core.Movies.Indexers.Legacy.IndexerDefinition;
using LegacyIndexerRepository = NzbDrone.Core.Movies.Indexers.Legacy.IndexerRepository;
using LegacyNewznabSettings = NzbDrone.Core.Movies.Indexers.Legacy.Newznab.NewznabSettings;

namespace NzbDrone.Core.Test.IndexerTests
{
    // Porting step 6: Prowlarr's indexer layer stores its rows in the tables from migration 243
    // (PlattarrIndexers, PlattarrIndexerStatus, IndexerProxies, IndexerHistory,
    // IndexerDefinitionVersions) beside the legacy movie indexer tables.
    [TestFixture]
    public class PlattarrIndexerTablesFixture : DbTest<IndexerRepository, IndexerDefinition>
    {
        private static IndexerDefinition NewznabDefinition(string name)
        {
            return new IndexerDefinition
            {
                Name = name,
                Implementation = "Newznab",
                Enable = true,
                Added = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc),
                Settings = new NewznabSettings
                {
                    BaseUrl = "https://indexer.example",
                    ApiPath = "/api",
                    ApiKey = "abc123",
                    BaseSettings = new IndexerBaseSettings
                    {
                        QueryLimit = 100,
                        FailDownloads = new[] { (int)FailDownloads.Executables }
                    }
                }
            };
        }

        [Test]
        public void should_round_trip_prowlarr_newznab_indexer()
        {
            var definition = NewznabDefinition("Example");
            definition.EnableRss = false;

            Subject.Insert(definition);

            var stored = Subject.All().Single();

            stored.Settings.Should().BeOfType<NewznabSettings>();
            var settings = (NewznabSettings)stored.Settings;
            settings.BaseUrl.Should().Be("https://indexer.example");
            settings.BaseSettings.QueryLimit.Should().Be(100);
            settings.BaseSettings.FailDownloads.Should().BeEquivalentTo(new[] { (int)FailDownloads.Executables });

            stored.EnableRss.Should().BeFalse();
            stored.EnableAutomaticSearch.Should().BeTrue();
            stored.EnableInteractiveSearch.Should().BeTrue();
            stored.Priority.Should().Be(25);
        }

        [Test]
        public void should_round_trip_torrent_consumer_settings()
        {
            var definition = NewznabDefinition("Torrents");
            definition.Implementation = "Torznab";
            definition.Settings = new TorznabSettings
            {
                BaseUrl = "https://tracker.example",
                ApiPath = "/api",
                TorrentBaseSettings = new IndexerTorrentBaseSettings
                {
                    MinimumSeeders = 3,
                    SeedRatio = 1.5,
                    RejectBlocklistedTorrentHashesWhileGrabbing = true
                }
            };

            Subject.Insert(definition);

            var settings = (TorznabSettings)Subject.All().Single().Settings;
            settings.TorrentBaseSettings.MinimumSeeders.Should().Be(3);
            settings.TorrentBaseSettings.SeedRatio.Should().Be(1.5);
            settings.TorrentBaseSettings.RejectBlocklistedTorrentHashesWhileGrabbing.Should().BeTrue();
        }

        [Test]
        public void minimum_seeders_should_default_to_one()
        {
            new IndexerTorrentBaseSettings().MinimumSeeders.Should().Be(IndexerDefaults.MINIMUM_SEEDERS);
        }

        [Test]
        public void should_keep_legacy_and_prowlarr_indexers_apart()
        {
            var legacyRepository = Mocker.Resolve<LegacyIndexerRepository>();
            legacyRepository.Insert(new LegacyIndexerDefinition
            {
                Name = "Legacy",
                Implementation = "Newznab",
                ConfigContract = nameof(LegacyNewznabSettings),
                EnableRss = true,
                Settings = new LegacyNewznabSettings { BaseUrl = "https://legacy.example", ApiPath = "/api" }
            });

            Subject.Insert(NewznabDefinition("Example"));

            legacyRepository.All().Single().Settings.Should().BeOfType<LegacyNewznabSettings>();
            Subject.All().Single().Settings.Should().BeOfType<NewznabSettings>();
        }

        [Test]
        public void should_store_indexer_status_cookies()
        {
            var repository = Mocker.Resolve<IndexerStatusRepository>();
            repository.Insert(new IndexerStatus
            {
                ProviderId = 1,
                Cookies = new Dictionary<string, string> { { "session", "xyz" } },
                CookiesExpirationDate = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)
            });

            var stored = repository.FindByProviderId(1);
            stored.Cookies.Should().ContainKey("session").WhoseValue.Should().Be("xyz");
        }

        [Test]
        public void should_count_indexer_history_by_event_type()
        {
            var repository = Mocker.Resolve<IndexerHistoryRepository>();
            var now = DateTime.UtcNow;

            repository.InsertMany(new List<IndexerHistoryItem>
            {
                new() { IndexerId = 1, Date = now, EventType = IndexerHistoryEventType.IndexerQuery, Successful = true },
                new() { IndexerId = 1, Date = now, EventType = IndexerHistoryEventType.IndexerRss, Successful = true },
                new() { IndexerId = 1, Date = now, EventType = IndexerHistoryEventType.ReleaseGrabbed, Successful = true },
                new() { IndexerId = 2, Date = now, EventType = IndexerHistoryEventType.IndexerQuery, Successful = true },
                new() { IndexerId = 1, Date = now.AddDays(-3), EventType = IndexerHistoryEventType.IndexerQuery, Successful = true }
            });

            repository.CountSince(1, now.AddHours(-1), new List<IndexerHistoryEventType> { IndexerHistoryEventType.IndexerQuery, IndexerHistoryEventType.IndexerRss })
                .Should().Be(2);
        }

        [Test]
        public void should_store_indexer_proxy()
        {
            var repository = Mocker.Resolve<IndexerProxyRepository>();
            repository.Insert(new IndexerProxyDefinition
            {
                Name = "Proxy",
                Implementation = "Http",
                Tags = new HashSet<int> { 1 },
                Settings = new HttpSettings { Host = "proxy.local", Port = 8888 }
            });

            var stored = repository.All().Single();
            stored.Settings.Should().BeOfType<HttpSettings>();
            stored.Enable.Should().BeTrue();
        }
    }
}
