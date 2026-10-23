using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    // TV port, step 7 (docs/module-interface.md, "TV step 7, part 2: as built"). The series version of
    // the AutoTagging table: the same four columns, in the TV module's own table because its conditions
    // (network, series type, original country) and its target (series tags) are not the movie module's.
    //
    // A new table only: the migration changes no existing row.
    [Migration(8)]
    public class tv_auto_tagging : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("Tv_AutoTagging")
                .WithColumn("Name").AsString().Unique()
                .WithColumn("Specifications").AsString().WithDefaultValue("[]")
                .WithColumn("RemoveTagsAutomatically").AsBoolean().WithDefaultValue(false)
                .WithColumn("Tags").AsString().WithDefaultValue("[]");
        }
    }
}
