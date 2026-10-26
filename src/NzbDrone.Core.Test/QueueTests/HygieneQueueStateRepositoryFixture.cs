using System;
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
                RemainingSize = 0,
                LastSeenAt = lastStrike,
                LastStrikeAt = lastStrike
            });

            var state = Subject.Find(7, "isolated-hash");

            state.ReasonCode.Should().Be("import-blocked");
            state.Strikes.Should().Be(2);
            state.MediaKind.Should().Be(2);
            state.LastStrikeAt.Should().NotBeNull();
        }
    }
}
