using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(23)]
    public class hygiene_queue_safeguards : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("HygieneRules")
                .AddColumn("IgnoredDownloads").AsString().Nullable()
                .AddColumn("FailedImportPatternMode").AsString().NotNullable().WithDefaultValue("all")
                .AddColumn("FailedImportPatterns").AsString().Nullable()
                .AddColumn("MinimumCompletionPercent").AsInt32().NotNullable().WithDefaultValue(0)
                .AddColumn("MaximumCompletionPercent").AsInt32().NotNullable().WithDefaultValue(100)
                .AddColumn("MinimumTorrentSizeBytes").AsInt64().NotNullable().WithDefaultValue(0)
                .AddColumn("MaximumTorrentSizeBytes").AsInt64().NotNullable().WithDefaultValue(0)
                .AddColumn("MaximumDownloadHours").AsInt32().NotNullable().WithDefaultValue(0)
                .AddColumn("IgnoreWhileAlternateSpeedActive").AsBoolean().NotNullable().WithDefaultValue(true);
        }
    }
}
