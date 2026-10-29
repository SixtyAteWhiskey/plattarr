using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(18)]
    public class hygiene_replacement_outcomes : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("HygieneActions")
                .AddColumn("ReplacementSearchCommandId").AsInt32().Nullable()
                .AddColumn("ReplacementTargetIds").AsString().Nullable()
                .AddColumn("ReplacementSearchStatus").AsString().Nullable()
                .AddColumn("ReplacementGrabCount").AsInt32().Nullable()
                .AddColumn("ReplacementSearchEndedAt").AsDateTime().Nullable();

            Create.Index("IX_HygieneActions_ReplacementCommand")
                .OnTable("HygieneActions")
                .OnColumn("ReplacementSearchCommandId").Ascending();
        }
    }
}
