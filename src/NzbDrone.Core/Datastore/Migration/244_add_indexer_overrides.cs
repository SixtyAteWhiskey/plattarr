using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    // Per-media-type indexer overrides (porting step 7, docs/module-interface.md
    // "Indexer settings"). Step 8 folds this into 001_initial.
    [Migration(244)]
    public class add_indexer_overrides : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("IndexerOverrides")
                  .WithColumn("IndexerId").AsInt32().NotNullable()
                  .WithColumn("MediaKind").AsInt32().NotNullable()
                  .WithColumn("MultiLanguages").AsString().NotNullable().WithDefaultValue("[]")
                  .WithColumn("RequiredFlags").AsString().NotNullable().WithDefaultValue("[]")
                  .WithColumn("RemoveYear").AsBoolean().Nullable()
                  .WithColumn("Categories").AsString().NotNullable().WithDefaultValue("[]");

            Create.Index("IX_IndexerOverrides_IndexerId_MediaKind").OnTable("IndexerOverrides")
                  .OnColumn("IndexerId").Ascending()
                  .OnColumn("MediaKind").Ascending()
                  .WithOptions().Unique();
        }
    }
}
