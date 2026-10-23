using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    // Migration 005 adds the TV decision settings to databases that already hold release profiles
    // and indexer overrides. Existing rows keep today's behavior: nothing restricted, no pack-only age.
    [TestFixture]
    public class TvDecisionSettingsFixture : MigrationTest<tv_decision_settings>
    {
        private IDirectDataMapper GivenExistingRows()
        {
            return WithMigrationTestDb(m =>
            {
                m.Insert.IntoTable("ReleaseProfiles").Row(new
                {
                    Required = "[\"x265\"]",
                    Ignored = "[]",
                    Tags = "[1]",
                    Name = "Profile",
                    Enabled = true,
                    IndexerId = 3
                });

                m.Insert.IntoTable("IndexerOverrides").Row(new
                {
                    IndexerId = 3,
                    MediaKind = 1,
                    MultiLanguages = "[]",
                    RequiredFlags = "[32]",
                    RemoveYear = true,
                    Categories = "[2040]"
                });
            });
        }

        [Test]
        public void should_keep_a_release_profile_and_default_the_tv_settings_to_off()
        {
            var db = GivenExistingRows();

            var row = db.Query("SELECT \"Name\", \"Required\", \"IndexerId\", \"AirDateRestriction\", \"AirDateGracePeriod\", \"AllowSeasonPackWithoutAllEpisodesAired\" FROM \"ReleaseProfiles\"").Single();

            row["Name"].Should().Be("Profile");
            row["Required"].Should().Be("[\"x265\"]");
            Convert.ToInt32(row["IndexerId"]).Should().Be(3);
            Convert.ToBoolean(row["AirDateRestriction"]).Should().BeFalse();
            Convert.ToInt32(row["AirDateGracePeriod"]).Should().Be(0);
            Convert.ToBoolean(row["AllowSeasonPackWithoutAllEpisodesAired"]).Should().BeFalse();
        }

        [Test]
        public void should_keep_an_indexer_override_and_default_the_single_episode_age_to_off()
        {
            var db = GivenExistingRows();

            var row = db.Query("SELECT \"IndexerId\", \"MediaKind\", \"RequiredFlags\", \"RemoveYear\", \"Categories\", \"SeasonSearchMaximumSingleEpisodeAge\" FROM \"IndexerOverrides\"").Single();

            Convert.ToInt32(row["IndexerId"]).Should().Be(3);
            Convert.ToInt32(row["MediaKind"]).Should().Be(1);
            row["RequiredFlags"].Should().Be("[32]");
            Convert.ToBoolean(row["RemoveYear"]).Should().BeTrue();
            row["Categories"].Should().Be("[2040]");
            Convert.ToInt32(row["SeasonSearchMaximumSingleEpisodeAge"]).Should().Be(0);
        }

        [Test]
        public void should_add_the_columns_to_empty_tables()
        {
            var db = WithMigrationTestDb();

            db.Query("SELECT \"AirDateRestriction\", \"AirDateGracePeriod\", \"AllowSeasonPackWithoutAllEpisodesAired\" FROM \"ReleaseProfiles\"").Should().BeEmpty();
            db.Query("SELECT \"SeasonSearchMaximumSingleEpisodeAge\" FROM \"IndexerOverrides\"").Should().BeEmpty();
        }
    }
}
