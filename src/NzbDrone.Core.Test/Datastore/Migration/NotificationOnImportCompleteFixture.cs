using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    // Migration 007 adds the On Import Complete flag to databases that already hold notifications.
    // Every existing notification keeps its flags and stays off for the new event.
    [TestFixture]
    public class NotificationOnImportCompleteFixture : MigrationTest<notification_on_import_complete>
    {
        [Test]
        public void should_keep_existing_notifications_and_default_the_new_flag_to_off()
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
                    OnManualInteractionRequired = true
                });
            });

            var row = db.Query("SELECT \"Name\", \"OnGrab\", \"OnDownload\", \"OnUpgrade\", \"Tags\", \"OnManualInteractionRequired\", \"OnImportComplete\" FROM \"Notifications\"").Single();

            row["Name"].Should().Be("Hook");
            Convert.ToBoolean(row["OnGrab"]).Should().BeTrue();
            Convert.ToBoolean(row["OnDownload"]).Should().BeTrue();
            Convert.ToBoolean(row["OnUpgrade"]).Should().BeTrue();
            row["Tags"].Should().Be("[1]");
            Convert.ToBoolean(row["OnManualInteractionRequired"]).Should().BeTrue();
            Convert.ToBoolean(row["OnImportComplete"]).Should().BeFalse();
        }

        [Test]
        public void should_add_the_column_to_an_empty_table()
        {
            var db = WithMigrationTestDb();

            db.Query("SELECT \"OnImportComplete\" FROM \"Notifications\"").Should().BeEmpty();
        }
    }
}
