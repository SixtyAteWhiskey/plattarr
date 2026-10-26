using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(13)]
    public class hygiene_action_journal : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("HygieneActions")
                .WithColumn("DownloadClientId").AsInt32()
                .WithColumn("DownloadId").AsString()
                .WithColumn("MediaKind").AsInt32()
                .WithColumn("ReasonCode").AsString()
                .WithColumn("RuleId").AsInt32()
                .WithColumn("Title").AsString().Nullable()
                .WithColumn("Status").AsString()
                .WithColumn("Checkpoint").AsString()
                .WithColumn("RecoveryPath").AsString()
                .WithColumn("LastError").AsString().Nullable()
                .WithColumn("CreatedAt").AsDateTime()
                .WithColumn("UpdatedAt").AsDateTime();

            Create.Index("IX_HygieneActions_Client_Download_Status")
                .OnTable("HygieneActions")
                .OnColumn("DownloadClientId").Ascending()
                .OnColumn("DownloadId").Ascending()
                .OnColumn("Status").Ascending();
        }
    }
}
