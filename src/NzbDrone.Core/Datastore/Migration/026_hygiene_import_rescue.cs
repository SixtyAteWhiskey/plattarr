using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(26)]
    public class hygiene_import_rescue : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("HygieneRules")
                .AddColumn("ForceImportEnabled").AsBoolean().NotNullable().WithDefaultValue(false)
                .AddColumn("ForceImportMaxTries").AsInt32().NotNullable().WithDefaultValue(3);

            Create.TableForModel("HygieneImportRescues")
                .WithColumn("DownloadClientId").AsInt32()
                .WithColumn("DownloadId").AsString()
                .WithColumn("MediaKind").AsInt32()
                .WithColumn("RuleId").AsInt32()
                .WithColumn("TargetIds").AsString()
                .WithColumn("CandidateFingerprint").AsString()
                .WithColumn("Status").AsString()
                .WithColumn("Attempts").AsInt32()
                .WithColumn("CommandId").AsInt32().Nullable()
                .WithColumn("RequestedAt").AsDateTime().Nullable()
                .WithColumn("NextAttemptAt").AsDateTime().Nullable()
                .WithColumn("CreatedAt").AsDateTime()
                .WithColumn("UpdatedAt").AsDateTime();

            Create.Index("IX_HygieneImportRescues_Client_Download")
                .OnTable("HygieneImportRescues")
                .OnColumn("DownloadClientId").Ascending()
                .OnColumn("DownloadId").Ascending();
        }
    }
}
