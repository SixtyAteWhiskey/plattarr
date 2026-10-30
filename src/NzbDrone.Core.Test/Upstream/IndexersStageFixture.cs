using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Converters;
using NzbDrone.Core.IndexerProxies;
using NzbDrone.Core.IndexerProxies.FlareSolverr;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Definitions;
using NzbDrone.Core.Indexers.Definitions.Cardigann;
using NzbDrone.Core.Indexers.Overrides;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Upstream
{
    // The indexer stage against a migrated target. Prowlarr rows are shaped like the owner's
    // (session 60 read of a live copy, key sets only): Cardigann settings with definitionFile,
    // extraFieldData, baseSettings.limitsUnit and torrentBaseSettings.preferMagnetUrl and nothing
    // else; native indexers with NoAuthTorrentBaseSettings; one sync profile; a tagged
    // FlareSolverr proxy. Radarr rows are Prowlarr's synchronized Torznab copies.
    [TestFixture]
    public class IndexersStageFixture : DbTest
    {
        private const string Secret = UpstreamFixtureTest.FakeApiKey;

        private IndexerRepository _indexers;
        private IndexerProxyRepository _proxies;
        private IndexerOverrideRepository _overrides;
        private TagRepository _tags;

        [SetUp]
        public void SetUpStages()
        {
            _indexers = Mocker.Resolve<IndexerRepository>();
            _proxies = Mocker.Resolve<IndexerProxyRepository>();
            _overrides = Mocker.Resolve<IndexerOverrideRepository>();
            _tags = Mocker.Resolve<TagRepository>();

            Mocker.SetConstant<IIndexerRepository>(_indexers);
            Mocker.SetConstant<IIndexerProxyRepository>(_proxies);
            Mocker.SetConstant<IIndexerOverrideRepository>(_overrides);
            Mocker.SetConstant<ITagRepository>(_tags);
            Mocker.SetConstant<IEnumerable<IUpstreamCatalog>>(new IUpstreamCatalog[] { new RadarrCatalog(), new SonarrCatalog() });
            Mocker.SetConstant<IEnumerable<IImportStage>>(new IImportStage[] { Mocker.Resolve<TagsStage>(), Mocker.Resolve<IndexersStage>() });
        }

        private static string Cardigann(string definition, string torrentBaseSettings = "{\"preferMagnetUrl\":false}", string extra = "{\"sort\":\"0\",\"apikey\":\"" + Secret + "\"}")
        {
            return "{\"definitionFile\":\"" + definition + "\",\"extraFieldData\":" + extra + ",\"baseUrl\":\"https://tracker.invalid/\","
                + "\"baseSettings\":{\"limitsUnit\":0},\"torrentBaseSettings\":" + torrentBaseSettings + "}";
        }

        private static string NoAuth()
        {
            return "{\"baseSettings\":{\"limitsUnit\":0},\"torrentBaseSettings\":{\"preferMagnetUrl\":false}}";
        }

        private static string Profile(int id, string name, int rss, int automatic, int interactive, int minimumSeeders)
        {
            return UpstreamFixtureTest.Insert(
                "AppSyncProfiles",
                "Id, Name, EnableRss, EnableInteractiveSearch, EnableAutomaticSearch, MinimumSeeders",
                $"{id}, {UpstreamFixtureTest.Sql(name)}, {rss}, {interactive}, {automatic}, {minimumSeeders}");
        }

        private static string Indexer(int id, string name, string implementation, string contract, string settings, int enable = 1, int profile = 1, string tags = "[]", int downloadClientId = 0)
        {
            return UpstreamFixtureTest.Insert(
                "Indexers",
                "Id, Name, Implementation, Settings, ConfigContract, Enable, Priority, Added, Redirect, AppProfileId, Tags, DownloadClientId",
                $"{id}, {UpstreamFixtureTest.Sql(name)}, {UpstreamFixtureTest.Sql(implementation)}, {UpstreamFixtureTest.Sql(settings)}, {UpstreamFixtureTest.Sql(contract)}, {enable}, 30, '2026-01-02 03:04:05Z', 0, {profile}, {UpstreamFixtureTest.Sql(tags)}, {downloadClientId}");
        }

        private static string Proxy(int id, string name, string implementation, string contract, string tags)
        {
            return UpstreamFixtureTest.Insert(
                "IndexerProxies",
                "Id, Name, Settings, Implementation, ConfigContract, Tags",
                $"{id}, {UpstreamFixtureTest.Sql(name)}, '{{\"host\":\"http://flaresolverr.invalid:8191/\",\"requestTimeout\":60}}', {UpstreamFixtureTest.Sql(implementation)}, {UpstreamFixtureTest.Sql(contract)}, {UpstreamFixtureTest.Sql(tags)}");
        }

        private static string RadarrApplication(bool rejectBlocklisted = true)
        {
            var settings = "{\"prowlarrUrl\":\"http://prowlarr.invalid:9696\",\"baseUrl\":\"http://radarr.invalid\",\"apiKey\":\"" + Secret + "\","
                + "\"syncCategories\":[2000,2010,2020,2030,2040,2045,2050,2060,2070,2080,2090],"
                + "\"syncRejectBlocklistedTorrentHashesWhileGrabbing\":" + (rejectBlocklisted ? "true" : "false") + "}";

            return UpstreamFixtureTest.Insert(
                "Applications",
                "Id, Name, Implementation, Settings, ConfigContract, SyncLevel, Tags",
                $"1, 'Radarr', 'Radarr', {UpstreamFixtureTest.Sql(settings)}, 'RadarrSettings', 2, '[]'");
        }

        // Radarr's synchronized copy of a Prowlarr indexer.
        private static string RadarrCopy(int id, string name, int prowlarrId, string extra = "", int rss = 1, int automatic = 1, int interactive = 1, string tags = "[]", string baseUrl = null)
        {
            var settings = "{\"baseUrl\":\"" + (baseUrl ?? $"http://prowlarr.invalid:9696/{prowlarrId}/") + "\",\"apiPath\":\"/api\",\"apiKey\":\"" + Secret + "\","
                + "\"minimumSeeders\":1,\"seedCriteria\":{},\"rejectBlocklistedTorrentHashesWhileGrabbing\":true"
                + (extra.Length > 0 ? "," + extra : ",\"categories\":[2000,2040],\"multiLanguages\":[],\"requiredFlags\":[],\"failDownloads\":[],\"removeYear\":false") + "}";

            return UpstreamFixtureTest.Insert(
                "Indexers",
                "Id, Name, Implementation, Settings, ConfigContract, EnableRss, EnableAutomaticSearch, EnableInteractiveSearch, Priority, Tags, DownloadClientId",
                $"{id}, {UpstreamFixtureTest.Sql(name)}, 'Torznab', {UpstreamFixtureTest.Sql(settings)}, 'TorznabSettings', {rss}, {automatic}, {interactive}, 25, {UpstreamFixtureTest.Sql(tags)}, 0");
        }

        private static string Mapping(int id, int prowlarrId, int radarrId)
        {
            return UpstreamFixtureTest.Mapping(id, prowlarrId, 1, radarrId);
        }

        // Prowlarr's Sonarr application (Prowlarr's default TV and anime sync categories).
        private static string SonarrApplication(int id = 2)
        {
            var settings = "{\"prowlarrUrl\":\"http://prowlarr.invalid:9696\",\"baseUrl\":\"http://sonarr.invalid\",\"apiKey\":\"" + Secret + "\","
                + "\"syncCategories\":[5000,5010,5020,5030,5040,5045,5050,5090],\"animeSyncCategories\":[5070],"
                + "\"syncAnimeStandardFormatSearch\":false,\"syncRejectBlocklistedTorrentHashesWhileGrabbing\":false}";

            return UpstreamFixtureTest.Insert(
                "Applications",
                "Id, Name, Implementation, Settings, ConfigContract, SyncLevel, Tags",
                $"{id}, 'Sonarr', 'Sonarr', {UpstreamFixtureTest.Sql(settings)}, 'SonarrSettings', 2, '[]'");
        }

        // Sonarr's synchronized copy of a Prowlarr indexer (Sonarr 4 Torznab settings).
        private static string SonarrCopy(int id, string name, int prowlarrId, string extra = "", int seasonSearchMaximumSingleEpisodeAge = 0)
        {
            var settings = "{\"baseUrl\":\"http://prowlarr.invalid:9696/" + prowlarrId + "/\",\"apiPath\":\"/api\",\"apiKey\":\"" + Secret + "\","
                + "\"minimumSeeders\":1,\"seedCriteria\":{},\"rejectBlocklistedTorrentHashesWhileGrabbing\":false"
                + (extra.Length > 0 ? "," + extra : ",\"categories\":[5000,5040],\"animeCategories\":[5070],\"animeStandardFormatSearch\":false,\"multiLanguages\":[],\"failDownloads\":[]") + "}";

            return UpstreamFixtureTest.Insert(
                "Indexers",
                "Id, Name, Implementation, Settings, ConfigContract, EnableRss, EnableAutomaticSearch, EnableInteractiveSearch, Priority, Tags, DownloadClientId, SeasonSearchMaximumSingleEpisodeAge",
                $"{id}, {UpstreamFixtureTest.Sql(name)}, 'Torznab', {UpstreamFixtureTest.Sql(settings)}, 'TorznabSettings', 1, 1, 1, 25, '[]', 0, {seasonSearchMaximumSingleEpisodeAge}");
        }

        private ImportResult Run(IEnumerable<string> prowlarrRows, IEnumerable<string> radarrRows = null, bool strict = false, IEnumerable<string> sonarrRows = null)
        {
            var snapshots = new List<SourceSnapshot>();

            if (prowlarrRows != null)
            {
                snapshots.Add(UpstreamFixtureTest.ReadSnapshot(new ProwlarrAdapter(), GetTestPath("Files/Upstream/prowlarr-44.schema.sql"), 44, prowlarrRows, TempFolder + "/prowlarr"));
            }

            if (radarrRows != null)
            {
                snapshots.Add(UpstreamFixtureTest.ReadSnapshot(new RadarrAdapter(), GetTestPath("Files/Upstream/radarr-242.schema.sql"), 242, radarrRows, TempFolder + "/radarr"));
            }

            if (sonarrRows != null)
            {
                snapshots.Add(UpstreamFixtureTest.ReadSnapshot(new SonarrAdapter(), GetTestPath("Files/Upstream/sonarr-217.schema.sql"), 217, sonarrRows, TempFolder + "/sonarr"));
            }

            return Mocker.Resolve<UpstreamImporter>().Run(snapshots, new ImportOptions { Strict = strict }, false);
        }

        private static void ShouldNotLeakSecrets(ImportResult result)
        {
            result.Warnings.Should().NotContain(w => w.Contains(Secret) || w.Contains("invalid"));
            result.Skips.Should().NotContain(s => (s.Detail ?? string.Empty).Contains(Secret) || (s.Detail ?? string.Empty).Contains("invalid"));
        }

        [Test]
        public void should_import_a_cardigann_indexer_with_the_sync_profile_folded_in()
        {
            var result = Run(new[]
            {
                Profile(1, "Standard", 0, 1, 0, 3),
                Indexer(5, "Tracker", "Cardigann", "CardigannSettings", Cardigann("tracker"))
            });

            var indexer = _indexers.All().Single();
            var settings = (CardigannSettings)indexer.Settings;

            indexer.Name.Should().Be("Tracker");
            indexer.Implementation.Should().Be("Cardigann");
            indexer.ConfigContract.Should().Be(nameof(CardigannSettings));
            indexer.Enable.Should().BeTrue();
            indexer.Priority.Should().Be(30);
            indexer.Added.Should().Be(new System.DateTime(2026, 1, 2, 3, 4, 5, System.DateTimeKind.Utc));
            indexer.EnableRss.Should().BeFalse();
            indexer.EnableAutomaticSearch.Should().BeTrue();
            indexer.EnableInteractiveSearch.Should().BeFalse();
            settings.DefinitionFile.Should().Be("tracker");
            settings.ExtraFieldData.Keys.Should().BeEquivalentTo("sort", "apikey");
            settings.ExtraFieldData["apikey"].ToString().Should().Be(Secret);
            settings.TorrentBaseSettings.MinimumSeeders.Should().Be(3);

            result.IdMap.Resolve("Prowlarr", "Indexers", 5).Should().Be(indexer.Id);
            result.Tables.Single(t => t.App == "Prowlarr" && t.Table == "AppSyncProfiles").Imported.Should().Be(1);
            result.Warnings.Should().ContainSingle(w => w.Contains("Cardigann") && w.Contains("Tracker (tracker)"));
            ShouldNotLeakSecrets(result);
        }

        [Test]
        public void should_take_prowlarrs_app_minimum_seeders_over_the_sync_profile()
        {
            Run(new[]
            {
                Profile(1, "Standard", 1, 1, 1, 3),
                Indexer(5, "Tracker", "Cardigann", "CardigannSettings", Cardigann("tracker", "{\"preferMagnetUrl\":true,\"appMinimumSeeders\":7,\"seedRatio\":1.5}"))
            });

            var settings = (CardigannSettings)_indexers.All().Single().Settings;

            settings.TorrentBaseSettings.MinimumSeeders.Should().Be(7);
            settings.TorrentBaseSettings.SeedRatio.Should().Be(1.5);
            settings.TorrentBaseSettings.PreferMagnetUrl.Should().BeTrue();
        }

        [Test]
        public void should_import_a_native_indexer_and_skip_an_implementation_plattarr_does_not_have()
        {
            var result = Run(new[]
            {
                Profile(1, "Standard", 1, 1, 1, 1),
                Indexer(11, "Knaben", "Knaben", "NoAuthTorrentBaseSettings", NoAuth()),
                Indexer(12, "Gone", "OpenTracker", "NoAuthTorrentBaseSettings", NoAuth())
            });

            _indexers.All().Select(i => i.Name).Should().Equal("Knaben");
            result.Skips.Should().ContainSingle(s => s.Table == "Indexers" && s.SourceId == 12 && s.Reason == SkipReason.UnknownProvider && s.Name == "Gone");
            result.Tables.Single(t => t.App == "Prowlarr" && t.Table == "Indexers").Should().Match<ReconciliationRow>(r => r.Read == 2 && r.Imported == 1 && r.Skipped == 1);
        }

        [TestCase("CGPeers", "GazelleSettings")]
        [TestCase("FunFile", "UserPassTorrentBaseSettings")]
        [TestCase("HDSpace", "HDSpaceSettings")]
        [TestCase("HDTorrents", "UserPassTorrentBaseSettings")]
        [TestCase("PornoLab", "PornoLabSettings")]
        public void should_still_import_native_indexers_with_cardigann_definition_ids(string implementation, string contract)
        {
            var result = Run(new[]
            {
                Profile(1, "Standard", 1, 1, 1, 1),
                Indexer(11, "Existing native", implementation, contract, NoAuth())
            });

            var indexer = _indexers.All().Single();

            indexer.Implementation.Should().Be(implementation);
            indexer.ConfigContract.Should().Be(contract);
            result.Skips.Should().NotContain(s => s.Table == "Indexers" && s.Reason == SkipReason.UnknownProvider);
        }

        [Test]
        public void should_convert_a_funfile_upstream_row_after_native_import()
        {
            const string nativeSettings = """
                {"baseUrl":"https://www.funfile.org/","username":"sample-user","password":"FAKE-PASSWORD","torrentBaseSettings":{"minimumSeeders":4}}
                """;
            var result = Run(new[]
            {
                Profile(1, "Standard", 1, 1, 1, 1),
                Indexer(11, "Existing FunFile", "FunFile", "UserPassTorrentBaseSettings", nativeSettings)
            });

            var imported = _indexers.All().Single();
            imported.Implementation.Should().Be("FunFile");

            var serializer = new ProviderSettingConverter();
            var converted = (CardigannSettings)serializer.Parse(
                FunFileSettingsConverter.Convert(serializer.Serialize(imported.Settings)), typeof(CardigannSettings));

            converted.DefinitionFile.Should().Be("funfile");
            converted.ExtraFieldData["username"].Should().Be("sample-user");
            converted.ExtraFieldData["password"].Should().Be("FAKE-PASSWORD");
            converted.TorrentBaseSettings.MinimumSeeders.Should().Be(4);
            result.Skips.Should().NotContain(s => s.Table == "Indexers");
        }

        [Test]
        public void should_keep_a_disabled_indexer_disabled_with_the_profile_flags()
        {
            Run(new[]
            {
                Profile(1, "Standard", 1, 1, 1, 1),
                Indexer(5, "Off", "Cardigann", "CardigannSettings", Cardigann("off"), enable: 0)
            });

            var indexer = _indexers.All().Single();

            indexer.Enable.Should().BeFalse();
            indexer.EnableRss.Should().BeTrue();
        }

        [Test]
        public void should_remap_tags_and_import_the_proxy()
        {
            var result = Run(new[]
            {
                UpstreamFixtureTest.Insert("Tags", "Id, Label", "4, 'flaresolverr'"),
                UpstreamFixtureTest.Insert("Tags", "Id, Label", "9, 'lazylibrarian'"),
                Profile(1, "Standard", 1, 1, 1, 1),
                Proxy(1, "FlareSolverr", "FlareSolverr", "FlareSolverrSettings", "[4]"),
                Proxy(2, "Onion", "Tor", "TorSettings", "[]"),
                Proxy(3, "Renamed", "HttpProxy", "HttpSettings", "[4]"),
                Indexer(5, "Tracker", "Cardigann", "CardigannSettings", Cardigann("tracker"), tags: "[4, 9, 77]")
            });

            var flaresolverr = _tags.All().Single(t => t.Label == "flaresolverr").Id;
            var lazylibrarian = _tags.All().Single(t => t.Label == "lazylibrarian").Id;
            var proxy = _proxies.All().Single();

            proxy.Name.Should().Be("FlareSolverr");
            proxy.Tags.Should().BeEquivalentTo(new[] { flaresolverr });
            ((FlareSolverrSettings)proxy.Settings).RequestTimeout.Should().Be(60);
            _indexers.All().Single().Tags.Should().BeEquivalentTo(new[] { flaresolverr, lazylibrarian });

            result.Skips.Where(s => s.Table == "IndexerProxies" && s.Reason == SkipReason.UnknownProvider).Select(s => s.SourceId).Should().BeEquivalentTo(new long?[] { 2, 3 });
            result.Warnings.Should().ContainSingle(w => w.Contains("\"Tracker\"") && w.Contains("1 tag(s)"));
        }

        [Test]
        public void should_name_settings_the_target_does_not_have_without_their_values()
        {
            var result = Run(new[]
            {
                Profile(1, "Standard", 1, 1, 1, 1),
                Indexer(11, "Knaben", "Knaben", "NoAuthTorrentBaseSettings", "{\"baseSettings\":{\"limitsUnit\":0},\"torrentBaseSettings\":{\"preferMagnetUrl\":false},\"cookie\":\"" + Secret + "\"}")
            });

            _indexers.All().Should().ContainSingle();
            result.Warnings.Should().ContainSingle(w => w.Contains("\"Knaben\"") && w.EndsWith(": cookie."));
            ShouldNotLeakSecrets(result);
        }

        [Test]
        public void should_skip_an_indexer_whose_settings_are_not_json()
        {
            var result = Run(new[]
            {
                Profile(1, "Standard", 1, 1, 1, 1),
                Indexer(5, "Broken", "Cardigann", "CardigannSettings", "{\"definitionFile\":"),
                Indexer(6, "Array", "Cardigann", "CardigannSettings", "[]"),
                Indexer(7, "Fine", "Cardigann", "CardigannSettings", Cardigann("fine"))
            });

            _indexers.All().Select(i => i.Name).Should().Equal("Fine");
            result.Skips.Where(s => s.Reason == SkipReason.Corrupt).Select(s => s.SourceId).Should().BeEquivalentTo(new long?[] { 5, 6 });
        }

        [Test]
        public void should_use_defaults_and_warn_when_the_sync_profile_is_missing_and_drop_an_unused_profile()
        {
            var result = Run(new[]
            {
                Profile(2, "Unused", 0, 0, 0, 9),
                Indexer(5, "Tracker", "Cardigann", "CardigannSettings", Cardigann("tracker"), profile: 1)
            });

            var indexer = _indexers.All().Single();

            indexer.EnableRss.Should().BeTrue();
            indexer.EnableAutomaticSearch.Should().BeTrue();
            indexer.EnableInteractiveSearch.Should().BeTrue();
            ((CardigannSettings)indexer.Settings).TorrentBaseSettings.MinimumSeeders.Should().Be(IndexerDefaults.MINIMUM_SEEDERS);
            result.Warnings.Should().Contain(w => w.Contains("\"Tracker\"") && w.Contains("sync profile that does not exist"));
            result.Skips.Should().ContainSingle(s => s.Table == "AppSyncProfiles" && s.SourceId == 2 && s.Reason == SkipReason.Obsolete);
        }

        [Test]
        public void should_match_radarrs_copies_and_point_them_at_the_prowlarr_indexer()
        {
            var result = Run(
                new[]
                {
                    Profile(1, "Standard", 1, 1, 1, 1),
                    RadarrApplication(),
                    Indexer(5, "Tracker", "Cardigann", "CardigannSettings", Cardigann("tracker")),
                    Mapping(1, 5, 20)
                },
                new[]
                {
                    RadarrCopy(20, "Tracker (Prowlarr)", 5),
                    RadarrCopy(21, "Hand Added", 0, baseUrl: "https://hand.invalid/api/torznab/")
                });

            var indexer = _indexers.All().Single();

            result.IdMap.Resolve("Radarr", "Indexers", 20).Should().Be(indexer.Id);
            result.IdMap.TryResolve("Radarr", "Indexers", 21, out _).Should().BeFalse();
            result.Skips.Should().ContainSingle(s => s.App == "Radarr" && s.SourceId == 21 && s.Reason == SkipReason.UnmatchedIndexer && s.Name == "Hand Added");
            result.Tables.Single(t => t.App == "Radarr" && t.Table == "Indexers").Should().Match<ReconciliationRow>(r => r.Read == 2 && r.Imported == 1 && r.Skipped == 1);
            _overrides.All().Should().BeEmpty();
            ShouldNotLeakSecrets(result);
        }

        [Test]
        public void should_list_prowlarrs_sync_tables_as_not_imported_and_pass_strict_mode()
        {
            var result = Run(
                new[]
                {
                    Profile(1, "Standard", 1, 1, 1, 1),
                    RadarrApplication(),
                    Indexer(5, "Tracker", "Cardigann", "CardigannSettings", Cardigann("tracker")),
                    Mapping(1, 5, 20)
                },
                new[] { RadarrCopy(20, "Tracker (Prowlarr)", 5) },
                strict: true);

            result.Outcome.Should().Be(ImportOutcome.Committed);
            result.Skips.Should().BeEmpty();
            result.UnreadTables.Should().Contain(t => t.App == "Prowlarr" && t.Name == "Applications" && t.RowCount == 1 && t.Reason == SkipReason.Obsolete);
            result.UnreadTables.Should().Contain(t => t.App == "Prowlarr" && t.Name == "ApplicationIndexerMapping" && t.RowCount == 1 && t.Reason == SkipReason.Obsolete);
            result.Tables.Should().NotContain(t => t.Table == "Applications" || t.Table == "ApplicationIndexerMapping");
        }

        [Test]
        public void should_carry_radarrs_fail_downloads_and_reject_blocklisted_into_the_indexer()
        {
            Run(
                new[]
                {
                    Profile(1, "Standard", 1, 1, 1, 1),
                    RadarrApplication(rejectBlocklisted: false),
                    Indexer(5, "Copied", "Cardigann", "CardigannSettings", Cardigann("copied")),
                    Indexer(6, "Not Copied", "Cardigann", "CardigannSettings", Cardigann("notcopied")),
                    Mapping(1, 5, 20)
                },
                new[] { RadarrCopy(20, "Copied (Prowlarr)", 5, "\"categories\":[2000],\"multiLanguages\":[],\"requiredFlags\":[],\"failDownloads\":[0,1],\"removeYear\":false") });

            var copied = (CardigannSettings)_indexers.All().Single(i => i.Name == "Copied").Settings;
            var notCopied = (CardigannSettings)_indexers.All().Single(i => i.Name == "Not Copied").Settings;

            copied.BaseSettings.FailDownloads.Should().Equal(0, 1);
            copied.TorrentBaseSettings.RejectBlocklistedTorrentHashesWhileGrabbing.Should().BeTrue();
            notCopied.BaseSettings.FailDownloads.Should().BeEmpty();
            notCopied.TorrentBaseSettings.RejectBlocklistedTorrentHashesWhileGrabbing.Should().BeFalse();
        }

        [Test]
        public void should_take_reject_blocklisted_from_the_radarr_application_when_there_is_no_copy()
        {
            Run(new[]
            {
                Profile(1, "Standard", 1, 1, 1, 1),
                RadarrApplication(rejectBlocklisted: true),
                Indexer(6, "Not Copied", "Cardigann", "CardigannSettings", Cardigann("notcopied"))
            });

            ((CardigannSettings)_indexers.All().Single().Settings).TorrentBaseSettings.RejectBlocklistedTorrentHashesWhileGrabbing.Should().BeTrue();
        }

        [Test]
        public void should_write_a_movies_override_for_what_radarrs_copy_set_by_hand()
        {
            var result = Run(
                new[]
                {
                    Profile(1, "Standard", 1, 1, 1, 1),
                    RadarrApplication(),
                    Indexer(5, "Tuned", "Cardigann", "CardigannSettings", Cardigann("tuned")),
                    Indexer(6, "Plain", "Cardigann", "CardigannSettings", Cardigann("plain")),
                    Mapping(1, 5, 20),
                    Mapping(2, 6, 21)
                },
                new[]
                {
                    RadarrCopy(20, "Tuned (Prowlarr)", 5, "\"categories\":[2000,100001],\"multiLanguages\":[3],\"requiredFlags\":[1],\"failDownloads\":[],\"removeYear\":true"),
                    RadarrCopy(21, "Plain (Prowlarr)", 6)
                });

            var tuned = _indexers.All().Single(i => i.Name == "Tuned").Id;
            var indexerOverride = _overrides.All().Single();

            indexerOverride.IndexerId.Should().Be(tuned);
            indexerOverride.MediaKind.Should().Be(MediaKind.Movies);
            indexerOverride.MultiLanguages.Should().Equal(Language.Spanish.Id);
            indexerOverride.RequiredFlags.Should().Equal(1);
            indexerOverride.RemoveYear.Should().BeTrue();
            indexerOverride.Categories.Should().Equal(2000, 100001);
            result.Skips.Should().BeEmpty();
        }

        [Test]
        public void should_write_an_override_for_one_setting_alone()
        {
            Run(
                new[]
                {
                    Profile(1, "Standard", 1, 1, 1, 1),
                    RadarrApplication(),
                    Indexer(5, "Flags", "Cardigann", "CardigannSettings", Cardigann("flags")),
                    Indexer(6, "Year", "Cardigann", "CardigannSettings", Cardigann("year")),
                    Mapping(1, 5, 20),
                    Mapping(2, 6, 21)
                },
                new[]
                {
                    RadarrCopy(20, "Flags (Prowlarr)", 5, "\"categories\":[2000],\"multiLanguages\":[],\"requiredFlags\":[8],\"removeYear\":false"),
                    RadarrCopy(21, "Year (Prowlarr)", 6, "\"categories\":[2000],\"multiLanguages\":[],\"requiredFlags\":[],\"removeYear\":true")
                });

            var byIndexer = _overrides.All().ToDictionary(o => _indexers.Get(o.IndexerId).Name);

            byIndexer.Keys.Should().BeEquivalentTo("Flags", "Year");
            byIndexer["Flags"].RequiredFlags.Should().Equal(8);
            byIndexer["Flags"].RemoveYear.Should().BeNull();
            byIndexer["Flags"].Categories.Should().BeEmpty();
            byIndexer["Year"].RemoveYear.Should().BeTrue();
            byIndexer["Year"].RequiredFlags.Should().BeEmpty();
        }

        [Test]
        public void should_compare_radarrs_flags_with_what_prowlarr_pushed()
        {
            var result = Run(
                new[]
                {
                    Profile(1, "Standard", 1, 1, 1, 1),
                    RadarrApplication(),
                    Indexer(5, "Disabled Both", "Cardigann", "CardigannSettings", Cardigann("a"), enable: 0),
                    Indexer(6, "Differs", "Cardigann", "CardigannSettings", Cardigann("b")),
                    Mapping(1, 5, 20),
                    Mapping(2, 6, 21)
                },
                new[]
                {
                    RadarrCopy(20, "Disabled Both (Prowlarr)", 5, rss: 0, automatic: 0, interactive: 0),
                    RadarrCopy(21, "Differs (Prowlarr)", 6, rss: 0, automatic: 1, interactive: 1)
                });

            var flagWarnings = result.Warnings.Where(w => w.Contains("differs from Prowlarr")).ToList();

            flagWarnings.Should().ContainSingle();
            flagWarnings[0].Should().Contain("\"Differs (Prowlarr)\"").And.Contain("RSS off in Radarr, on in Prowlarr").And.NotContain("automatic");
            _indexers.All().Single(i => i.Name == "Differs").EnableRss.Should().BeTrue();
        }

        [Test]
        public void should_warn_about_radarr_tags_and_not_carry_them()
        {
            var result = Run(
                new[]
                {
                    Profile(1, "Standard", 1, 1, 1, 1),
                    RadarrApplication(),
                    Indexer(5, "Tracker", "Cardigann", "CardigannSettings", Cardigann("tracker")),
                    Mapping(1, 5, 20)
                },
                new[]
                {
                    UpstreamFixtureTest.Insert("Tags", "Id, Label", "3, 'kids'"),
                    RadarrCopy(20, "Tracker (Prowlarr)", 5, tags: "[\n  3\n]")
                });

            _indexers.All().Single().Tags.Should().BeEmpty();
            result.Warnings.Should().ContainSingle(w => w.Contains("\"Tracker (Prowlarr)\"") && w.Contains("has tags"));
        }

        [Test]
        public void should_skip_a_copy_whose_prowlarr_indexer_was_skipped()
        {
            var result = Run(
                new[]
                {
                    Profile(1, "Standard", 1, 1, 1, 1),
                    RadarrApplication(),
                    Indexer(12, "Gone", "OpenTracker", "NoAuthTorrentBaseSettings", NoAuth()),
                    Mapping(1, 12, 20)
                },
                new[] { RadarrCopy(20, "Gone (Prowlarr)", 12) });

            result.Skips.Should().ContainSingle(s => s.App == "Radarr" && s.SourceId == 20 && s.Reason == SkipReason.UnmatchedIndexer && s.Detail.Contains("not imported"));
        }

        [Test]
        public void should_skip_radarrs_indexers_without_a_prowlarr_database()
        {
            var result = Run(null, new[] { RadarrCopy(20, "Tracker (Prowlarr)", 5) });

            _indexers.All().Should().BeEmpty();
            result.Skips.Should().ContainSingle(s => s.App == "Radarr" && s.Table == "Indexers" && s.Reason == SkipReason.UnmatchedIndexer);
        }

        [Test]
        public void should_match_sonarrs_copies_and_keep_their_tv_settings_as_an_override()
        {
            var result = Run(
                new[]
                {
                    Profile(1, "Standard", 1, 1, 1, 1),
                    SonarrApplication(),
                    Indexer(5, "Tuned", "Cardigann", "CardigannSettings", Cardigann("tuned")),
                    Indexer(6, "Plain", "Cardigann", "CardigannSettings", Cardigann("plain")),
                    UpstreamFixtureTest.Mapping(1, 5, 2, 30),
                    UpstreamFixtureTest.Mapping(2, 6, 2, 31)
                },
                sonarrRows: new[]
                {
                    SonarrCopy(30, "Tuned (Prowlarr)", 5, "\"categories\":[5000,100002],\"animeCategories\":[5070],\"multiLanguages\":[3],\"requiredFlags\":[8],\"failDownloads\":[]", seasonSearchMaximumSingleEpisodeAge: 7),
                    SonarrCopy(31, "Plain (Prowlarr)", 6)
                });

            var tuned = _indexers.All().Single(i => i.Name == "Tuned").Id;
            var plain = _indexers.All().Single(i => i.Name == "Plain").Id;
            var indexerOverride = _overrides.All().Single();

            indexerOverride.IndexerId.Should().Be(tuned);
            indexerOverride.MediaKind.Should().Be(MediaKind.Tv);
            indexerOverride.MultiLanguages.Should().Equal(Language.Spanish.Id);

            // Sonarr's bit 8 is Internal; the global vocabulary's Internal is 32 (F4).
            indexerOverride.RequiredFlags.Should().Equal((int)IndexerFlags.G_Internal);
            indexerOverride.Categories.Should().Equal(5000, 100002);
            indexerOverride.SeasonSearchMaximumSingleEpisodeAge.Should().Be(7);
            indexerOverride.RemoveYear.Should().BeNull();

            result.IdMap.Resolve("Sonarr", "Indexers", 30).Should().Be(tuned);
            result.IdMap.Resolve("Sonarr", "Indexers", 31).Should().Be(plain);
            result.Tables.Should().ContainSingle(t => t.App == "Sonarr" && t.Table == "Indexers" && t.Read == 2 && t.Imported == 2 && t.Skipped == 0);
            result.Skips.Should().BeEmpty();
            ShouldNotLeakSecrets(result);
        }

        [Test]
        public void should_keep_a_movie_and_a_tv_override_for_one_prowlarr_indexer()
        {
            Run(
                new[]
                {
                    Profile(1, "Standard", 1, 1, 1, 1),
                    RadarrApplication(),
                    SonarrApplication(),
                    Indexer(5, "Both", "Cardigann", "CardigannSettings", Cardigann("both")),
                    Mapping(1, 5, 20),
                    UpstreamFixtureTest.Mapping(2, 5, 2, 30)
                },
                new[] { RadarrCopy(20, "Both (Prowlarr)", 5, "\"categories\":[2000],\"multiLanguages\":[],\"requiredFlags\":[],\"removeYear\":true") },
                sonarrRows: new[] { SonarrCopy(30, "Both (Prowlarr)", 5, seasonSearchMaximumSingleEpisodeAge: 3) });

            _overrides.All().Select(o => o.MediaKind).Should().BeEquivalentTo(new[] { MediaKind.Movies, MediaKind.Tv });
            _overrides.All().Single(o => o.MediaKind == MediaKind.Tv).SeasonSearchMaximumSingleEpisodeAge.Should().Be(3);
            _overrides.All().Single(o => o.MediaKind == MediaKind.Movies).RemoveYear.Should().BeTrue();
        }

        [Test]
        public void should_report_a_sonarr_copy_that_searches_anime_by_standard_numbers()
        {
            var result = Run(
                new[]
                {
                    Profile(1, "Standard", 1, 1, 1, 1),
                    SonarrApplication(),
                    Indexer(5, "Anime", "Cardigann", "CardigannSettings", Cardigann("anime")),
                    UpstreamFixtureTest.Mapping(1, 5, 2, 30)
                },
                sonarrRows: new[] { SonarrCopy(30, "Anime (Prowlarr)", 5, "\"categories\":[5000],\"animeCategories\":[5070,140679],\"animeStandardFormatSearch\":true,\"multiLanguages\":[]") });

            _overrides.All().Should().BeEmpty();
            result.Warnings.Should().ContainSingle(w => w.Contains("Anime Standard Format Search") && w.Contains("Anime (Prowlarr)"));
            result.Warnings.Should().ContainSingle(w => w.Contains("anime categories that Prowlarr does not sync"));
            ShouldNotLeakSecrets(result);
        }

        [Test]
        public void should_skip_sonarrs_indexers_without_a_prowlarr_database()
        {
            var result = Run(null, sonarrRows: new[] { SonarrCopy(30, "Tracker (Prowlarr)", 5) });

            _indexers.All().Should().BeEmpty();
            result.Skips.Should().ContainSingle(s => s.App == "Sonarr" && s.Table == "Indexers" && s.Reason == SkipReason.UnmatchedIndexer);
        }
    }
}
