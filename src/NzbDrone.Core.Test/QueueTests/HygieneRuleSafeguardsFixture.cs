using System.Collections.Generic;
using System.Linq;
using Moq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.Indexers;
using QueueItem = NzbDrone.Core.Queue.Queue;

namespace NzbDrone.Core.Test.QueueTests
{
    [TestFixture]
    public class HygieneRuleSafeguardsFixture
    {
        [TestCase("ABC123", null, null, null, null)]
        [TestCase("Movies", "Movies", null, null, null)]
        [TestCase("keep", null, "keep, other", null, null)]
        [TestCase("example.org", null, null, "https://tracker.example.org/announce", null)]
        [TestCase("My Client", null, null, null, "My Client")]
        public void ignores_exact_identity_or_tracker_host(string value, string category, string tags, string tracker, string client)
        {
            var item = new QueueItem
            {
                DownloadId = "abc123",
                DownloadCategory = category,
                DownloadTags = tags,
                DownloadTracker = tracker,
                DownloadClient = client
            };

            HygieneRuleSafeguards.Ignored(item, value).Should().BeTrue();
            HygieneRuleSafeguards.Ignored(item, "other.example.org").Should().BeFalse();
        }

        [TestCase("include", "title mismatch", true)]
        [TestCase("include", "not enough free space", false)]
        [TestCase("exclude", "title mismatch", false)]
        [TestCase("exclude", "not enough free space", true)]
        public void failed_import_filter_uses_plain_case_insensitive_substrings(string mode, string pattern, bool allowed)
        {
            var item = new QueueItem
            {
                StatusMessages = new List<TrackedDownloadStatusMessage>
                {
                    new("Import", "Movie TITLE MISMATCH; manual import required")
                }
            };
            var rule = new HygieneRule { FailedImportPatternMode = mode, FailedImportPatterns = pattern };

            (HygieneRuleSafeguards.HoldReason(item, rule, "import-blocked") == null).Should().Be(allowed);
        }

        [Test]
        public void completion_and_size_safeguards_hold_unknown_and_out_of_range_torrents()
        {
            var rule = new HygieneRule
            {
                MinimumCompletionPercent = 20,
                MaximumCompletionPercent = 80,
                MinimumTorrentSizeBytes = 1000
            };
            var item = new QueueItem { Size = 2000, SizeLeft = 1000 };

            HygieneRuleSafeguards.HoldReason(item, rule, "download-slow").Should().BeNull();
            item.SizeLeft = 1800;
            HygieneRuleSafeguards.HoldReason(item, rule, "download-slow").Should().Contain("completion");
            item.Size = 0;
            HygieneRuleSafeguards.HoldReason(item, rule, "download-slow").Should().Contain("unavailable");
        }

        [Test]
        public void slow_rule_holds_large_torrents_and_alternate_speed_mode()
        {
            var item = new QueueItem { Size = 2000, SizeLeft = 1000 };
            var rule = new HygieneRule { MaximumTorrentSizeBytes = 1000 };

            HygieneRuleSafeguards.HoldReason(item, rule, "download-slow").Should().Contain("maximum");
            rule.MaximumTorrentSizeBytes = 0;
            item.DownloadClientAlternateSpeedActive = true;
            HygieneRuleSafeguards.HoldReason(item, rule, "download-slow").Should().Contain("alternate speed");
            rule.IgnoreWhileAlternateSpeedActive = false;
            HygieneRuleSafeguards.HoldReason(item, rule, "download-slow").Should().BeNull();
        }

        [Test]
        public void privacy_specific_rule_wins_within_the_same_media_and_client_scope()
        {
            var rules = new[]
            {
                new HygieneRule { Id = 1, ReasonCode = "download-stalled", TorrentPrivacyScope = "all" },
                new HygieneRule { Id = 2, ReasonCode = "download-stalled", TorrentPrivacyScope = "private" },
                new HygieneRule { Id = 3, ReasonCode = "download-stalled", TorrentPrivacyScope = "public" }
            };
            var service = new HygieneRuleService(new Mock<IHygieneRuleRepository>().Object, NLog.LogManager.GetCurrentClassLogger());
            var item = new QueueItem
            {
                Protocol = DownloadProtocol.Torrent,
                RemoteRelease = new NzbDrone.Core.Movies.Parser.Model.RemoteMovie
                {
                    Release = new NzbDrone.Core.Parser.Model.ReleaseInfo { IndexerPrivacy = IndexerPrivacy.Private }
                }
            };

            HygieneRuleSafeguards.MatchForItem(rules, service, "download-stalled", 0, item).Id.Should().Be(2);
            item.RemoteRelease.Release.IndexerPrivacy = IndexerPrivacy.SemiPrivate;
            HygieneRuleSafeguards.MatchForItem(rules, service, "download-stalled", 0, item).Id.Should().Be(2);
            item.RemoteRelease.Release.IndexerPrivacy = IndexerPrivacy.Public;
            HygieneRuleSafeguards.MatchForItem(rules, service, "download-stalled", 0, item).Id.Should().Be(3);
            item.RemoteRelease.Release = null;
            HygieneRuleSafeguards.MatchForItem(rules, service, "download-stalled", 0, item).Id.Should().Be(1);
            HygieneRuleSafeguards.HoldReason(item, rules.First(), "download-stalled").Should().BeNull();
        }
    }
}
