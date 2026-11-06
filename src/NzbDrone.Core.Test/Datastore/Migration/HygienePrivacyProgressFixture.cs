using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class HygienePrivacyProgressFixture : MigrationTest<hygiene_privacy_progress>
    {
        [Test]
        public void existing_rules_keep_their_progress_behavior_and_have_an_all_privacy_scope()
        {
            using var connection = WithDapperMigrationTestDb(migration =>
            {
                migration.Insert.IntoTable("HygieneRules").Row(new
                {
                    ReasonCode = "download-stalled",
                    MediaKind = 0,
                    DownloadClientId = 0,
                    Enabled = true,
                    StrikeLimit = 3,
                    StrikeIntervalMinutes = 5
                });
                migration.Insert.IntoTable("HygieneRules").Row(new
                {
                    ReasonCode = "download-slow",
                    MediaKind = 0,
                    DownloadClientId = 0,
                    Enabled = true,
                    StrikeLimit = 3,
                    StrikeIntervalMinutes = 5,
                    MinimumDownloadRateKiBps = 100
                });
                migration.Insert.IntoTable("HygieneQueueStates").Row(new
                {
                    DownloadClientId = 1,
                    DownloadId = "existing-job",
                    MediaKind = 1,
                    ReasonCode = "download-stalled",
                    Strikes = 2,
                    RemainingSize = 1000,
                    LastSeenAt = "2026-11-05"
                });
            });

            var rows = connection.Query("SELECT \"ReasonCode\", \"TorrentPrivacyScope\", \"ResetStrikesOnProgress\", \"MinimumProgressToResetBytes\" FROM \"HygieneRules\"").AsList();
            rows.Should().HaveCount(2);
            foreach (var row in rows)
            {
                ((string)row.TorrentPrivacyScope).Should().Be("all");
                ((long)row.MinimumProgressToResetBytes).Should().Be(0);
                ((long)row.ResetStrikesOnProgress).Should().Be(row.ReasonCode == "download-stalled" ? 1 : 0);
            }

            connection.Execute("INSERT INTO \"HygieneRules\" (\"ReasonCode\", \"MediaKind\", \"DownloadClientId\", \"Enabled\", \"StrikeLimit\", \"StrikeIntervalMinutes\", \"TorrentPrivacyScope\") VALUES ('download-stalled', 0, 0, 0, 3, 5, 'private')");
            connection.QuerySingle<int>("SELECT COUNT(*) FROM \"HygieneRules\" WHERE \"ReasonCode\" = 'download-stalled'").Should().Be(2);
            var state = connection.QuerySingle("SELECT \"Strikes\", \"ProgressBaselineRemainingSize\", \"ConsecutiveRuleId\" FROM \"HygieneQueueStates\"");
            ((long)state.Strikes).Should().Be(2);
            ((object)state.ProgressBaselineRemainingSize).Should().BeNull();
            ((object)state.ConsecutiveRuleId).Should().BeNull();
        }
    }
}
