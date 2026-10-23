using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    // TV port, step 5 (docs/module-interface.md, "TV step 5: as built"). The tables the TV module's
    // extras write: metadata consumer settings and the three kinds of extra file next to an
    // episode (metadata, subtitle and other). Columns are Sonarr's property names so the importer
    // maps them one to one, with SeriesId in place of the movie module's MovieId.
    //
    // New tables only: the migration changes no existing row. The movie module's equivalents are
    // Movies_Metadata, Movies_MetadataFiles, Movies_SubtitleFiles and Movies_ExtraFiles.
    [Migration(6)]
    public class tv_extras_tables : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("Tv_Metadata")
                .WithColumn("Enable").AsBoolean()
                .WithColumn("Name").AsString()
                .WithColumn("Implementation").AsString()
                .WithColumn("Settings").AsString()
                .WithColumn("ConfigContract").AsString();

            Create.TableForModel("Tv_MetadataFiles")
                .WithColumn("SeriesId").AsInt32().NotNullable()
                .WithColumn("Consumer").AsString().NotNullable()
                .WithColumn("Type").AsInt32().NotNullable()
                .WithColumn("RelativePath").AsString().NotNullable()
                .WithColumn("LastUpdated").AsDateTimeOffset().NotNullable()
                .WithColumn("SeasonNumber").AsInt32().Nullable()
                .WithColumn("EpisodeFileId").AsInt32().Nullable()
                .WithColumn("Hash").AsString().Nullable()
                .WithColumn("Added").AsDateTimeOffset().Nullable()
                .WithColumn("Extension").AsString().NotNullable();

            Create.TableForModel("Tv_ExtraFiles")
                .WithColumn("SeriesId").AsInt32().NotNullable()
                .WithColumn("SeasonNumber").AsInt32().Nullable()
                .WithColumn("EpisodeFileId").AsInt32().Nullable()
                .WithColumn("RelativePath").AsString().NotNullable()
                .WithColumn("Extension").AsString().NotNullable()
                .WithColumn("Added").AsDateTimeOffset().NotNullable()
                .WithColumn("LastUpdated").AsDateTimeOffset().NotNullable();

            Create.TableForModel("Tv_SubtitleFiles")
                .WithColumn("SeriesId").AsInt32().NotNullable()
                .WithColumn("SeasonNumber").AsInt32().Nullable()
                .WithColumn("EpisodeFileId").AsInt32().Nullable()
                .WithColumn("RelativePath").AsString().NotNullable()
                .WithColumn("Extension").AsString().NotNullable()
                .WithColumn("Added").AsDateTimeOffset().NotNullable()
                .WithColumn("LastUpdated").AsDateTimeOffset().NotNullable()
                .WithColumn("Language").AsInt32().NotNullable()
                .WithColumn("LanguageTags").AsString().Nullable()
                .WithColumn("Title").AsString().Nullable()
                .WithColumn("Copy").AsInt32().NotNullable().WithDefaultValue(0);
        }
    }
}
