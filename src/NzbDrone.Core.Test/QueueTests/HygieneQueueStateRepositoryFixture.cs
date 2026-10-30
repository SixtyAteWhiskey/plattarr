using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.QueueTests
{
    [TestFixture]
    public class HygieneQueueStateRepositoryFixture : DbTest<HygieneQueueStateRepository, HygieneQueueState>
    {
        [Test]
        public void strikes_should_round_trip_with_the_download_identity()
        {
            var lastStrike = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
            Subject.Insert(new HygieneQueueState
            {
                DownloadClientId = 7,
                DownloadId = "isolated-hash",
                MediaKind = 2,
                ReasonCode = "import-blocked",
                Strikes = 2,
                UnstableStrikes = 11,
                UnstableRuleId = 8,
                RemainingSize = 0,
                LastSeenAt = lastStrike,
                LastStrikeAt = lastStrike,
                LastUnstableStrikeAt = lastStrike
            });

            var state = Subject.Find(7, "isolated-hash");

            state.ReasonCode.Should().Be("import-blocked");
            state.Strikes.Should().Be(2);
            state.UnstableStrikes.Should().Be(11);
            state.UnstableRuleId.Should().Be(8);
            state.MediaKind.Should().Be(2);
            state.LastStrikeAt.Should().NotBeNull();
            state.LastUnstableStrikeAt.Should().NotBeNull();
        }

        [Test]
        public void identity_query_returns_only_requested_client_and_download_pairs()
        {
            var first = Insert(7, "shared");
            Insert(8, "shared");
            var second = Insert(8, "other");

            Subject.FindByIdentities(new[] { (7, "shared"), (7, "shared"), (8, "other") })
                .Select(state => state.Id)
                .Should().BeEquivalentTo(new[] { first.Id, second.Id });
            Subject.FindByIdentities(Array.Empty<(int, string)>()).Should().BeEmpty();
        }

        [Test]
        public void identity_query_batches_large_queues()
        {
            var last = Insert(7, "download-200");
            var identities = Enumerable.Range(0, 201).Select(index => (7, $"download-{index}"));

            Subject.FindByIdentities(identities).Should().ContainSingle().Which.Id.Should().Be(last.Id);
        }

        [Test]
        public void expired_delete_excludes_states_at_or_after_the_cutoff()
        {
            var cutoff = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
            var expired = Insert(7, "expired");
            expired.LastSeenAt = cutoff.AddSeconds(-1);
            Subject.Update(expired);
            var boundary = Insert(7, "boundary");
            boundary.LastSeenAt = cutoff;
            Subject.Update(boundary);
            var recent = Insert(7, "recent");

            Subject.DeleteExpired(cutoff);

            Subject.All().Select(state => state.Id).Should().BeEquivalentTo(new[] { boundary.Id, recent.Id });
        }

        private HygieneQueueState Insert(int clientId, string downloadId)
        {
            return Subject.Insert(new HygieneQueueState
            {
                DownloadClientId = clientId,
                DownloadId = downloadId,
                LastSeenAt = DateTime.UtcNow
            });
        }
    }
}
