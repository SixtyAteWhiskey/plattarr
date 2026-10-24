using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    // Music port, step 4 (docs/module-interface.md, "Music step 4: as built"). One setting the Music
    // decision specifications read that has no column in the shared schema. It is nullable, as in
    // Lidarr, where null is off, so the migration changes no existing row.
    //
    //   IndexerOverrides.EarlyReleaseLimit     days a release may be published before its album's
    //                                          release date (Lidarr's indexer EarlyReleaseLimit);
    //                                          null is off
    //
    // Movies and Tv ignore the column, and it is set only on Music rows.
    [Migration(10)]
    public class music_decision_settings : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("IndexerOverrides").AddColumn("EarlyReleaseLimit").AsInt32().Nullable();
        }
    }
}
