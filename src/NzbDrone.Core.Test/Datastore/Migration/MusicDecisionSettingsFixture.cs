using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    // Migration 010 adds the Music early release limit to databases that already hold indexer
    // overrides. Existing rows keep today's behavior: no limit.
    [TestFixture]
    public class MusicDecisionSettingsFixture : MigrationTest<music_decision_settings>
    {
        [Test]
        public void should_keep_an_indexer_override_and_default_the_early_release_limit_to_off()
        {
            var db = WithMigrationTestDb(m =>
            {
                m.Insert.IntoTable("IndexerOverrides").Row(new
                {
                    IndexerId = 3,
                    MediaKind = 1,
                    MultiLanguages = "[]",
                    RequiredFlags = "[32]",
                    RemoveYear = true,
                    Categories = "[2040]",
                    SeasonSearchMaximumSingleEpisodeAge = 30
                });
            });

            var row = db.Query("SELECT \"IndexerId\", \"MediaKind\", \"RequiredFlags\", \"Categories\", \"SeasonSearchMaximumSingleEpisodeAge\", \"EarlyReleaseLimit\" FROM \"IndexerOverrides\"").Single();

            Convert.ToInt32(row["IndexerId"]).Should().Be(3);
            Convert.ToInt32(row["MediaKind"]).Should().Be(1);
            row["RequiredFlags"].Should().Be("[32]");
            row["Categories"].Should().Be("[2040]");
            Convert.ToInt32(row["SeasonSearchMaximumSingleEpisodeAge"]).Should().Be(30);
            (row["EarlyReleaseLimit"] is null or DBNull).Should().BeTrue("existing rows keep today's behavior: no limit");
        }

        [Test]
        public void should_add_the_column_to_an_empty_table()
        {
            var db = WithMigrationTestDb();

            db.Query("SELECT \"EarlyReleaseLimit\" FROM \"IndexerOverrides\"").Should().BeEmpty();
        }
    }
}
