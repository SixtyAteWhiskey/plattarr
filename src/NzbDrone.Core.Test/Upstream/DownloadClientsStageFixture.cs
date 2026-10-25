using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Test.Framework;
using Importer = NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Upstream
{
    [TestFixture]
    public class DownloadClientsStageFixture : DbTest
    {
        private IDownloadClientRepository _clients;

        [SetUp]
        public void SetUpStages()
        {
            _clients = Mocker.Resolve<DownloadClientRepository>();
            Mocker.SetConstant<IDownloadClientRepository>(_clients);

            var tags = Mocker.Resolve<TagRepository>();
            Mocker.SetConstant<ITagRepository>(tags);
            Mocker.SetConstant<IEnumerable<Importer.IImportStage>>(new Importer.IImportStage[]
            {
                Mocker.Resolve<Importer.TagsStage>(),
                Mocker.Resolve<Importer.DownloadClientsStage>()
            });
        }

        private Importer.SourceSnapshot RadarrSnapshot()
        {
            return UpstreamFixtureTest.ReadSnapshot(
                new Importer.RadarrAdapter(),
                GetTestPath("Files/Upstream/radarr-242.schema.sql"),
                242,
                UpstreamFixtureTest.RadarrRows,
                TempFolder);
        }

        private Importer.SourceSnapshot SonarrSnapshot()
        {
            var settings = "{\"host\":\"qbit.invalid\",\"port\":8080,\"useSsl\":false,\"username\":\"user\",\"password\":\"FAKE-PASSWORD\",\"tvCategory\":\"sonarr-tv\",\"tvImportedCategory\":\"sonarr-done\",\"recentTvPriority\":2,\"olderTvPriority\":3,\"initialState\":0,\"sequentialOrder\":false,\"firstAndLast\":false,\"contentLayout\":0}";
            var rows = new[]
            {
                UpstreamFixtureTest.Insert("Tags", "Id, Label", "1, 'Kids'"),
                UpstreamFixtureTest.Insert(
                    "DownloadClients",
                    "Id, Enable, Name, Implementation, Settings, ConfigContract, Priority, RemoveCompletedDownloads, RemoveFailedDownloads, Tags",
                    "2, 1, 'qBittorrent TV', 'QBittorrent', " + UpstreamFixtureTest.Sql(settings) + ", 'QBittorrentSettings', 1, 1, 1, '[1]'")
            };

            return UpstreamFixtureTest.ReadSnapshot(
                new Importer.SonarrAdapter(),
                GetTestPath("Files/Upstream/sonarr-217.schema.sql"),
                217,
                rows,
                TempFolder);
        }

        private Importer.SourceSnapshot ProwlarrSnapshot()
        {
            var matchingSettings = "{\"host\":\"qbit.invalid\",\"port\":8080,\"useSsl\":false,\"username\":\"user\",\"password\":\"FAKE-PASSWORD\"}";
            var unmatchedSettings = "{\"host\":\"other.invalid\",\"port\":8081,\"useSsl\":false}";
            var rows = new[]
            {
                UpstreamFixtureTest.Insert(
                    "DownloadClients",
                    "Id, Enable, Name, Implementation, Settings, ConfigContract, Priority, Categories",
                    "5, 1, 'Prowlarr qBittorrent', 'QBittorrent', " + UpstreamFixtureTest.Sql(matchingSettings) + ", 'QBittorrentSettings', 1, '[]'"),
                UpstreamFixtureTest.Insert(
                    "DownloadClients",
                    "Id, Enable, Name, Implementation, Settings, ConfigContract, Priority, Categories",
                    "6, 1, 'Prowlarr only qBittorrent', 'QBittorrent', " + UpstreamFixtureTest.Sql(unmatchedSettings) + ", 'QBittorrentSettings', 1, '[]'")
            };

            return UpstreamFixtureTest.ReadSnapshot(
                new Importer.ProwlarrAdapter(),
                GetTestPath("Files/Upstream/prowlarr-44.schema.sql"),
                44,
                rows,
                TempFolder);
        }

        [Test]
        public void should_merge_radarr_and_sonarr_clients_map_prowlarr_and_ensure_every_media_category()
        {
            var result = Mocker.Resolve<Importer.UpstreamImporter>().Run(
                new[] { RadarrSnapshot(), ProwlarrSnapshot(), SonarrSnapshot() },
                new Importer.ImportOptions(),
                false);

            result.Outcome.Should().Be(Importer.ImportOutcome.Committed);
            result.IdMap.Resolve("Radarr", "DownloadClients", 1).Should().Be(result.IdMap.Resolve("Sonarr", "DownloadClients", 2));
            result.IdMap.Resolve("Prowlarr", "DownloadClients", 5).Should().Be(result.IdMap.Resolve("Radarr", "DownloadClients", 1));
            result.Skips.Should().ContainSingle(s => s.App == "Prowlarr" && s.Table == "DownloadClients" && s.SourceId == 6 && s.Reason == Importer.SkipReason.NoMediaType);

            var client = _clients.All().Should().ContainSingle().Which;
            var settings = client.Settings.Should().BeAssignableTo<IModuleCategorySettings>().Subject;
            settings.ModuleCategories.Select(c => c.Kind).Should().BeEquivalentTo(new[] { MediaKind.Movies, MediaKind.Tv, MediaKind.Music });
            settings.For(MediaKind.Movies).Category.Should().Be("radarr");
            settings.For(MediaKind.Tv).Category.Should().Be("sonarr-tv");
            settings.For(MediaKind.Music).Category.Should().Be("plattarr-music");

            result.Tables.Should().Contain(t => t.App == "Prowlarr" && t.Table == "DownloadClients" && t.Read == 2 && t.Imported == 1 && t.Skipped == 1);
            result.Tables.Should().Contain(t => t.App == "Radarr" && t.Table == "DownloadClients" && t.Read == 1 && t.Imported == 1 && t.Skipped == 0);
            result.Tables.Should().Contain(t => t.App == "Sonarr" && t.Table == "DownloadClients" && t.Read == 1 && t.Imported == 1 && t.Skipped == 0);
        }
    }
}
