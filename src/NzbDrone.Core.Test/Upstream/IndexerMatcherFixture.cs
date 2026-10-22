using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Upstream
{
    // The rows are laid out in UpstreamFixtureTest: Radarr indexers 1 and 2 were
    // synchronized from Prowlarr 13 and 37; Radarr 3's URL names Prowlarr 41, which only
    // Sonarr received; Radarr 4's URL names a Prowlarr indexer that does not exist;
    // Radarr 5 was added by hand.
    [TestFixture]
    public class IndexerMatcherFixture : UpstreamFixtureTest
    {
        private SourceRow RadarrIndexer(SourceSnapshot radarr, long id)
        {
            return radarr.Rows("Indexers").Single(r => r.GetLong("Id") == id);
        }

        [TestCase(1, 13)]
        [TestCase(2, 37)]
        public void should_match_a_synchronized_indexer_and_confirm_it_against_the_sync_record(long radarrId, long prowlarrId)
        {
            var radarr = ReadRadarr();
            var matcher = new IndexerMatcher(ReadProwlarr());

            var match = matcher.Match(RadarrIndexer(radarr, radarrId));

            match.Matched.Should().BeTrue();
            match.ProwlarrIndexerId.Should().Be(prowlarrId);
            match.Confirmed.Should().BeTrue();
        }

        [Test]
        public void should_not_let_another_apps_sync_record_confirm_a_match()
        {
            // Radarr 3's URL ends in 41. Prowlarr has indexer 41 and a sync record for it, but the
            // record belongs to Sonarr, whose indexer 3 is a different row from Radarr's indexer 3.
            var radarr = ReadRadarr();
            var matcher = new IndexerMatcher(ReadProwlarr());

            var match = matcher.Match(RadarrIndexer(radarr, 3));

            match.Matched.Should().BeFalse();
            match.Reason.Should().Contain("sync record");
        }

        [Test]
        public void should_not_match_an_indexer_prowlarr_does_not_have()
        {
            var radarr = ReadRadarr();
            var matcher = new IndexerMatcher(ReadProwlarr());

            var match = matcher.Match(RadarrIndexer(radarr, 4));

            match.Matched.Should().BeFalse();
            match.Reason.Should().Contain("no indexer");
        }

        [Test]
        public void should_not_match_an_indexer_added_by_hand()
        {
            var radarr = ReadRadarr();
            var matcher = new IndexerMatcher(ReadProwlarr());

            var match = matcher.Match(RadarrIndexer(radarr, 5));

            match.Matched.Should().BeFalse();
            match.Reason.Should().Contain("does not end in an indexer ID");
        }

        [Test]
        public void should_not_match_when_prowlarr_has_no_radarr_application()
        {
            var radarr = ReadRadarr();

            // Sonarr and Lidarr applications only; the indexer and mapping rows are the same.
            var prowlarr = ReadProwlarrOnly(
                ProwlarrIndexer(13, "Tracker One", "Cardigann", "CardigannSettings"),
                Application(2, "Sonarr", "Sonarr"),
                Mapping(1, 13, 2, 1));

            var match = new IndexerMatcher(prowlarr).Match(RadarrIndexer(radarr, 1));

            match.Matched.Should().BeFalse();
            match.Reason.Should().Contain("no Radarr application");
        }

        [Test]
        public void should_match_on_the_url_alone_when_prowlarr_kept_no_sync_records()
        {
            var radarr = ReadRadarr();

            var prowlarr = ReadProwlarrOnly(
                ProwlarrIndexer(13, "Tracker One", "Cardigann", "CardigannSettings"),
                Application(1, "Radarr", "Radarr"));

            var match = new IndexerMatcher(prowlarr).Match(RadarrIndexer(radarr, 1));

            match.Matched.Should().BeTrue();
            match.ProwlarrIndexerId.Should().Be(13);
            match.Confirmed.Should().BeFalse();
        }

        [Test]
        public void should_not_match_a_native_radarr_indexer()
        {
            var radarr = ReadRadarr(
                Insert(
                    "Indexers",
                    "Id, Name, Implementation, Settings, ConfigContract, EnableRss, EnableAutomaticSearch, EnableInteractiveSearch, Priority, Tags, DownloadClientId",
                    "6, 'Native', 'SomeNativeIndexer', '{\"baseUrl\":\"http://prowlarr.invalid:9696/13/\"}', 'SomeNativeIndexerSettings', 1, 1, 1, 25, '[]', 0"));

            var match = new IndexerMatcher(ReadProwlarr()).Match(RadarrIndexer(radarr, 6));

            match.Matched.Should().BeFalse();
            match.Reason.Should().Contain("Torznab or Newznab");
        }

        [Test]
        public void should_accept_newznab_and_a_url_base_in_front_of_the_id()
        {
            var radarr = ReadRadarr(
                Insert(
                    "Indexers",
                    "Id, Name, Implementation, Settings, ConfigContract, EnableRss, EnableAutomaticSearch, EnableInteractiveSearch, Priority, Tags, DownloadClientId",
                    "7, 'Usenet One', 'Newznab', '{\"baseUrl\":\"http://prowlarr.invalid:9696/prowlarr/13\"}', 'NewznabSettings', 1, 1, 1, 25, '[]', 0"));

            var prowlarr = ReadProwlarrOnly(
                ProwlarrIndexer(13, "Tracker One", "Cardigann", "CardigannSettings"),
                Application(1, "Radarr", "Radarr"),
                Mapping(1, 13, 1, 7));

            var match = new IndexerMatcher(prowlarr).Match(RadarrIndexer(radarr, 7));

            match.ProwlarrIndexerId.Should().Be(13);
            match.Confirmed.Should().BeTrue();
        }

        [Test]
        public void should_never_put_a_url_or_key_in_the_reason()
        {
            var radarr = ReadRadarr();
            var matcher = new IndexerMatcher(ReadProwlarr());

            foreach (var row in radarr.Rows("Indexers"))
            {
                var reason = matcher.Match(row).Reason;

                if (reason != null)
                {
                    reason.Should().NotContain("prowlarr.invalid").And.NotContain(FakeApiKey);
                }
            }
        }

        [Test]
        public void should_match_exactly_the_indexers_the_fixture_synchronized()
        {
            var radarr = ReadRadarr();
            var matcher = new IndexerMatcher(ReadProwlarr());

            radarr.Rows("Indexers")
                .Where(r => matcher.Match(r).Matched)
                .Select(r => r.GetLong("Id"))
                .Should().Equal(1L, 2L);
        }
    }
}
