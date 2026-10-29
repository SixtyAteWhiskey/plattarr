using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(19)]
    public class hygiene_replacement_search_option : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("HygieneRules")
                .AddColumn("SearchForReplacement").AsBoolean().NotNullable().WithDefaultValue(true);

            Alter.Table("HygieneActions")
                .AddColumn("SearchForReplacement").AsBoolean().NotNullable().WithDefaultValue(true);
        }
    }
}
