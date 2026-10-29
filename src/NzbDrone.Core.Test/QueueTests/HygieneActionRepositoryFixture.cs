using System;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.QueueTests
{
    [TestFixture]
    public class HygieneActionRepositoryFixture : DbTest<HygieneActionRepository, HygieneAction>
    {
        [Test]
        public void latest_action_is_scoped_to_client_download_and_reason()
        {
            var first = Insert(7, "shared-id", "client-failed");
            Insert(8, "shared-id", "client-failed");
            Insert(7, "other-id", "client-failed");
            Insert(7, "shared-id", "download-stalled");
            var latest = Insert(7, "shared-id", "client-failed");

            Subject.FindLatest(7, "shared-id", "client-failed").Id.Should().Be(latest.Id);
            Subject.FindLatest(7, "shared-id", "download-stalled").ReasonCode.Should().Be("download-stalled");
            Subject.FindLatest(7, "missing", "client-failed").Should().BeNull();
            first.Id.Should().BeLessThan(latest.Id);
        }

        private HygieneAction Insert(int clientId, string downloadId, string reasonCode)
        {
            return Subject.Insert(new HygieneAction
            {
                DownloadClientId = clientId,
                DownloadId = downloadId,
                MediaKind = 1,
                ReasonCode = reasonCode,
                RuleId = 1,
                Status = "completed",
                Checkpoint = "replacement-requested",
                RecoveryPath = "synthetic fixture",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
    }
}
