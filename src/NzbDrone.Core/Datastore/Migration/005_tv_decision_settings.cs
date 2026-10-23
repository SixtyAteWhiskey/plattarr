using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    // TV port, step 4 (docs/module-interface.md, "TV step 4: as built"). Three settings the TV
    // decision specifications read that have no column in the shared schema. Each column has a
    // default that keeps today's behavior, so the migration changes no existing row.
    //
    //   ReleaseProfiles.AirDateRestriction     reject a release published before the episode airs
    //   ReleaseProfiles.AirDateGracePeriod     hours before the air date a release is still accepted
    //   ReleaseProfiles.AllowSeasonPackWithoutAllEpisodesAired
    //                                          accept a season pack while episodes are unaired
    //   IndexerOverrides.SeasonSearchMaximumSingleEpisodeAge
    //                                          days after the last episode aired that an indexer's
    //                                          single episodes are refused in favor of a season
    //                                          pack; 0 is off
    //
    // Movies and Music ignore the release profile columns, and the override column is set only on
    // TV rows.
    [Migration(5)]
    public class tv_decision_settings : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("ReleaseProfiles").AddColumn("AirDateRestriction").AsBoolean().NotNullable().WithDefaultValue(false);
            Alter.Table("ReleaseProfiles").AddColumn("AirDateGracePeriod").AsInt32().NotNullable().WithDefaultValue(0);
            Alter.Table("ReleaseProfiles").AddColumn("AllowSeasonPackWithoutAllEpisodesAired").AsBoolean().NotNullable().WithDefaultValue(false);

            Alter.Table("IndexerOverrides").AddColumn("SeasonSearchMaximumSingleEpisodeAge").AsInt32().NotNullable().WithDefaultValue(0);
        }
    }
}
