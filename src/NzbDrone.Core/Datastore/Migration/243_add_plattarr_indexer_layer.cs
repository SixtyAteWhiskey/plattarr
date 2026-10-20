using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    // Tables for Prowlarr's indexer layer (porting step 6), in the shape of Prowlarr's
    // final schema at 26a605e. PlattarrIndexers and PlattarrIndexerStatus keep temporary
    // names because the legacy movie indexer layer owns Indexers and IndexerStatus until
    // step 7. Step 8 folds all of this into 001_initial.
    [Migration(243)]
    public class add_plattarr_indexer_layer : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("PlattarrIndexers")
                  .WithColumn("Name").AsString().Unique()
                  .WithColumn("Implementation").AsString()
                  .WithColumn("Settings").AsString().Nullable()
                  .WithColumn("ConfigContract").AsString().Nullable()
                  .WithColumn("Enable").AsBoolean().Nullable()
                  .WithColumn("Priority").AsInt32().NotNullable().WithDefaultValue(25)
                  .WithColumn("Added").AsDateTimeOffset()
                  .WithColumn("Redirect").AsBoolean().NotNullable().WithDefaultValue(false)
                  .WithColumn("Tags").AsString().Nullable()
                  .WithColumn("DownloadClientId").AsInt32().WithDefaultValue(0)
                  .WithColumn("EnableRss").AsBoolean().NotNullable().WithDefaultValue(true)
                  .WithColumn("EnableAutomaticSearch").AsBoolean().NotNullable().WithDefaultValue(true)
                  .WithColumn("EnableInteractiveSearch").AsBoolean().NotNullable().WithDefaultValue(true);

            Create.TableForModel("PlattarrIndexerStatus")
                  .WithColumn("ProviderId").AsInt32().NotNullable().Unique()
                  .WithColumn("InitialFailure").AsDateTimeOffset().Nullable()
                  .WithColumn("MostRecentFailure").AsDateTimeOffset().Nullable()
                  .WithColumn("EscalationLevel").AsInt32().NotNullable()
                  .WithColumn("DisabledTill").AsDateTimeOffset().Nullable()
                  .WithColumn("LastRssSyncReleaseInfo").AsString().Nullable()
                  .WithColumn("Cookies").AsString().Nullable()
                  .WithColumn("CookiesExpirationDate").AsDateTimeOffset().Nullable();

            Create.TableForModel("IndexerProxies")
                  .WithColumn("Name").AsString()
                  .WithColumn("Settings").AsString()
                  .WithColumn("Implementation").AsString()
                  .WithColumn("ConfigContract").AsString().Nullable()
                  .WithColumn("Tags").AsString().Nullable();

            // Every search, RSS fetch, and grab per indexer. Separate from the media History table.
            Create.TableForModel("IndexerHistory")
                  .WithColumn("IndexerId").AsInt32()
                  .WithColumn("Date").AsDateTimeOffset()
                  .WithColumn("Data").AsString()
                  .WithColumn("EventType").AsInt32().Nullable()
                  .WithColumn("DownloadId").AsString().Nullable().Indexed()
                  .WithColumn("Successful").AsBoolean().NotNullable().WithDefaultValue(true);

            // Query and grab limits count recent rows per indexer on every request.
            Create.Index("IX_IndexerHistory_IndexerId_Date").OnTable("IndexerHistory")
                  .OnColumn("IndexerId").Ascending()
                  .OnColumn("Date").Ascending();

            Create.TableForModel("IndexerDefinitionVersions")
                  .WithColumn("DefinitionId").AsString().NotNullable().Unique()
                  .WithColumn("File").AsString().NotNullable().Unique()
                  .WithColumn("Sha").AsString().Nullable()
                  .WithColumn("LastUpdated").AsDateTimeOffset().Nullable();
        }
    }
}
