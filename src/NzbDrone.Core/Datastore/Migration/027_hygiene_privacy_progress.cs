using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(27)]
    public class hygiene_privacy_progress : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("HygieneRules")
                .AddColumn("TorrentPrivacyScope").AsString().NotNullable().WithDefaultValue("all")
                .AddColumn("ResetStrikesOnProgress").AsBoolean().NotNullable().WithDefaultValue(true)
                .AddColumn("MinimumProgressToResetBytes").AsInt64().NotNullable().WithDefaultValue(0);

            Execute.Sql("UPDATE \"HygieneRules\" SET \"ResetStrikesOnProgress\" = 0 WHERE \"ReasonCode\" = 'download-slow'");

            Delete.Index("IX_HygieneRules_Scope").OnTable("HygieneRules");
            Create.Index("IX_HygieneRules_Scope")
                .OnTable("HygieneRules")
                .OnColumn("ReasonCode").Ascending()
                .OnColumn("MediaKind").Ascending()
                .OnColumn("DownloadClientId").Ascending()
                .OnColumn("TorrentPrivacyScope").Ascending()
                .WithOptions().Unique();

            Alter.Table("HygieneQueueStates")
                .AddColumn("ProgressBaselineRemainingSize").AsInt64().Nullable()
                .AddColumn("ConsecutiveRuleId").AsInt32().Nullable();
        }
    }
}
