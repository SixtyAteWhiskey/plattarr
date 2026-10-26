using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(12)]
    public class hygiene_queue_rules : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("HygieneQueueStates")
                .WithColumn("DownloadClientId").AsInt32()
                .WithColumn("DownloadId").AsString()
                .WithColumn("MediaKind").AsInt32().WithDefaultValue(0)
                .WithColumn("ReasonCode").AsString().Nullable()
                .WithColumn("Strikes").AsInt32().WithDefaultValue(0)
                .WithColumn("RemainingSize").AsInt64().WithDefaultValue(0)
                .WithColumn("LastSeenAt").AsDateTime()
                .WithColumn("LastStrikeAt").AsDateTime().Nullable();

            Create.Index("IX_HygieneQueueStates_Client_Download")
                .OnTable("HygieneQueueStates")
                .OnColumn("DownloadClientId").Ascending()
                .OnColumn("DownloadId").Ascending()
                .WithOptions().Unique();

            Create.TableForModel("HygieneRules")
                .WithColumn("ReasonCode").AsString()
                .WithColumn("MediaKind").AsInt32().WithDefaultValue(0)
                .WithColumn("DownloadClientId").AsInt32().WithDefaultValue(0)
                .WithColumn("Enabled").AsBoolean().WithDefaultValue(false)
                .WithColumn("StrikeLimit").AsInt32().WithDefaultValue(3)
                .WithColumn("StrikeIntervalMinutes").AsInt32().WithDefaultValue(5);

            Create.Index("IX_HygieneRules_Scope")
                .OnTable("HygieneRules")
                .OnColumn("ReasonCode").Ascending()
                .OnColumn("MediaKind").Ascending()
                .OnColumn("DownloadClientId").Ascending()
                .WithOptions().Unique();
        }
    }
}
