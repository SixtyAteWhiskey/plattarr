using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    // Migration 011 adds the album delete, download failure and track retag flags to databases that
    // already hold notifications. Every existing notification keeps its flags and stays off for the new events.
    [TestFixture]
    public class NotificationMusicEventsFixture : MigrationTest<notification_music_events>
    {
        [Test]
        public void should_keep_existing_notifications_and_default_the_new_flags_to_off()
        {
            var db = WithMigrationTestDb(m =>
            {
                m.Insert.IntoTable("Notifications").Row(new
                {
                    Name = "Hook",
                    OnGrab = true,
                    OnDownload = true,
                    OnRename = false,
                    OnUpgrade = true,
                    Settings = "{}",
                    Implementation = "Webhook",
                    ConfigContract = "WebhookSettings",
                    Tags = "[1]",
                    OnManualInteractionRequired = true,
                    OnImportComplete = true
                });
            });

            var row = db.Query("SELECT \"Name\", \"OnGrab\", \"OnDownload\", \"OnUpgrade\", \"Tags\", \"OnManualInteractionRequired\", \"OnImportComplete\", \"OnAlbumDelete\", \"OnDownloadFailure\", \"OnTrackRetag\" FROM \"Notifications\"").Single();

            row["Name"].Should().Be("Hook");
            Convert.ToBoolean(row["OnGrab"]).Should().BeTrue();
            Convert.ToBoolean(row["OnDownload"]).Should().BeTrue();
            Convert.ToBoolean(row["OnUpgrade"]).Should().BeTrue();
            row["Tags"].Should().Be("[1]");
            Convert.ToBoolean(row["OnManualInteractionRequired"]).Should().BeTrue();
            Convert.ToBoolean(row["OnImportComplete"]).Should().BeTrue();
            Convert.ToBoolean(row["OnAlbumDelete"]).Should().BeFalse();
            Convert.ToBoolean(row["OnDownloadFailure"]).Should().BeFalse();
            Convert.ToBoolean(row["OnTrackRetag"]).Should().BeFalse();
        }

        [Test]
        public void should_add_the_columns_to_an_empty_table()
        {
            var db = WithMigrationTestDb();

            db.Query("SELECT \"OnAlbumDelete\", \"OnDownloadFailure\", \"OnTrackRetag\" FROM \"Notifications\"").Should().BeEmpty();
        }
    }
}
