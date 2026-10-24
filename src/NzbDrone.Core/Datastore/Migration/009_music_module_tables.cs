using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    // Music port, step 1 (docs/module-interface.md, "Music step 1: as built"). Adds the tables the
    // Music module reads and writes. It upgrades a database in place and changes no existing row.
    //
    // The shared activity tables need nothing new: History.ItemId (migration 004) holds the album
    // of a row with ParentId the artist, and Blocklist.ItemIds the albums a release covered.
    //
    // Module tables, all prefixed Music_. Columns are Lidarr's property names so the importer maps
    // them one to one. A property that Lidarr stores as JSON is a string column here, and every
    // audio quality is stored as its catalog ID (0 for Unknown, 201 to 399 for a quality).
    //   Music_ArtistMetadata, Music_Artists, Music_Albums, Music_AlbumReleases, Music_Tracks,
    //   Music_TrackFiles, Music_QualityDefinitions, Music_QualityProfiles, Music_MetadataProfiles,
    //   Music_RootFolders, Music_NamingConfig.
    //
    // Deferred to the steps that need them: Music_MetadataFiles, Music_ExtraFiles, Music_Metadata
    // (import step), and the Music import list tables.
    [Migration(9)]
    public class music_module_tables : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("Music_ArtistMetadata")
                .WithColumn("ForeignArtistId").AsString().Unique()
                .WithColumn("OldForeignArtistIds").AsString().WithDefaultValue("[]")
                .WithColumn("Name").AsString()
                .WithColumn("Aliases").AsString().WithDefaultValue("[]")
                .WithColumn("Overview").AsString().Nullable()
                .WithColumn("Disambiguation").AsString().Nullable()
                .WithColumn("Type").AsString().Nullable()
                .WithColumn("Status").AsInt32().WithDefaultValue(0)
                .WithColumn("Images").AsString().WithDefaultValue("[]")
                .WithColumn("Links").AsString().WithDefaultValue("[]")
                .WithColumn("Genres").AsString().WithDefaultValue("[]")
                .WithColumn("Ratings").AsString().Nullable()
                .WithColumn("Members").AsString().WithDefaultValue("[]");

            Create.TableForModel("Music_Artists")
                .WithColumn("ArtistMetadataId").AsInt32().Unique()
                .WithColumn("CleanName").AsString().Nullable()
                .WithColumn("SortName").AsString().Nullable()
                .WithColumn("Monitored").AsBoolean()
                .WithColumn("MonitorNewItems").AsInt32().WithDefaultValue(0)
                .WithColumn("LastInfoSync").AsDateTimeOffset().Nullable()
                .WithColumn("Path").AsString()
                .WithColumn("Added").AsDateTimeOffset()
                .WithColumn("QualityProfileId").AsInt32()
                .WithColumn("MetadataProfileId").AsInt32().WithDefaultValue(0)
                .WithColumn("Tags").AsString().WithDefaultValue("[]")
                .WithColumn("AddOptions").AsString().Nullable();

            Create.Index("IX_Music_Artists_Path").OnTable("Music_Artists").OnColumn("Path").Ascending();
            Create.Index("IX_Music_Artists_CleanName").OnTable("Music_Artists").OnColumn("CleanName").Ascending();
            Create.Index("IX_Music_Artists_Monitored").OnTable("Music_Artists").OnColumn("Monitored").Ascending();

            Create.TableForModel("Music_Albums")
                .WithColumn("ArtistMetadataId").AsInt32()
                .WithColumn("ForeignAlbumId").AsString().Unique()
                .WithColumn("OldForeignAlbumIds").AsString().WithDefaultValue("[]")
                .WithColumn("Title").AsString()
                .WithColumn("Overview").AsString().Nullable()
                .WithColumn("Disambiguation").AsString().Nullable()
                .WithColumn("ReleaseDate").AsDateTimeOffset().Nullable()
                .WithColumn("Images").AsString().WithDefaultValue("[]")
                .WithColumn("Links").AsString().WithDefaultValue("[]")
                .WithColumn("Genres").AsString().WithDefaultValue("[]")
                .WithColumn("AlbumType").AsString().Nullable()
                .WithColumn("SecondaryTypes").AsString().WithDefaultValue("[]")
                .WithColumn("Ratings").AsString().Nullable()
                .WithColumn("LastSearchTime").AsDateTimeOffset().Nullable()
                .WithColumn("CleanTitle").AsString().Nullable()
                .WithColumn("ProfileId").AsInt32().WithDefaultValue(0)
                .WithColumn("Monitored").AsBoolean()
                .WithColumn("AnyReleaseOk").AsBoolean().WithDefaultValue(true)
                .WithColumn("LastInfoSync").AsDateTimeOffset().Nullable()
                .WithColumn("Added").AsDateTimeOffset()
                .WithColumn("AddOptions").AsString().Nullable();

            Create.Index("IX_Music_Albums_ArtistMetadataId").OnTable("Music_Albums").OnColumn("ArtistMetadataId").Ascending();
            Create.Index("IX_Music_Albums_CleanTitle").OnTable("Music_Albums").OnColumn("CleanTitle").Ascending();
            Create.Index("IX_Music_Albums_Monitored").OnTable("Music_Albums").OnColumn("Monitored").Ascending();
            Create.Index("IX_Music_Albums_ReleaseDate").OnTable("Music_Albums").OnColumn("ReleaseDate").Ascending();

            Create.TableForModel("Music_AlbumReleases")
                .WithColumn("AlbumId").AsInt32()
                .WithColumn("ForeignReleaseId").AsString().Unique()
                .WithColumn("OldForeignReleaseIds").AsString().WithDefaultValue("[]")
                .WithColumn("Title").AsString()
                .WithColumn("Status").AsString().Nullable()
                .WithColumn("Duration").AsInt32().WithDefaultValue(0)
                .WithColumn("Label").AsString().WithDefaultValue("[]")
                .WithColumn("Disambiguation").AsString().Nullable()
                .WithColumn("Country").AsString().WithDefaultValue("[]")
                .WithColumn("ReleaseDate").AsDateTimeOffset().Nullable()
                .WithColumn("Media").AsString().WithDefaultValue("[]")
                .WithColumn("TrackCount").AsInt32().WithDefaultValue(0)
                .WithColumn("Monitored").AsBoolean();

            Create.Index("IX_Music_AlbumReleases_AlbumId").OnTable("Music_AlbumReleases").OnColumn("AlbumId").Ascending();
            Create.Index("IX_Music_AlbumReleases_Monitored").OnTable("Music_AlbumReleases").OnColumn("Monitored").Ascending();

            Create.TableForModel("Music_Tracks")
                .WithColumn("ForeignTrackId").AsString().Unique()
                .WithColumn("OldForeignTrackIds").AsString().WithDefaultValue("[]")
                .WithColumn("ForeignRecordingId").AsString().WithDefaultValue("0")
                .WithColumn("OldForeignRecordingIds").AsString().WithDefaultValue("[]")
                .WithColumn("AlbumReleaseId").AsInt32()
                .WithColumn("ArtistMetadataId").AsInt32()
                .WithColumn("TrackNumber").AsString().Nullable()
                .WithColumn("AbsoluteTrackNumber").AsInt32().WithDefaultValue(0)
                .WithColumn("Title").AsString().Nullable()
                .WithColumn("Duration").AsInt32().WithDefaultValue(0)
                .WithColumn("Explicit").AsBoolean().WithDefaultValue(false)
                .WithColumn("Ratings").AsString().Nullable()
                .WithColumn("MediumNumber").AsInt32().WithDefaultValue(0)
                .WithColumn("TrackFileId").AsInt32().WithDefaultValue(0);

            Create.Index("IX_Music_Tracks_AlbumReleaseId").OnTable("Music_Tracks").OnColumn("AlbumReleaseId").Ascending();
            Create.Index("IX_Music_Tracks_ArtistMetadataId").OnTable("Music_Tracks").OnColumn("ArtistMetadataId").Ascending();
            Create.Index("IX_Music_Tracks_ForeignRecordingId").OnTable("Music_Tracks").OnColumn("ForeignRecordingId").Ascending();
            Create.Index("IX_Music_Tracks_TrackFileId").OnTable("Music_Tracks").OnColumn("TrackFileId").Ascending();

            Create.TableForModel("Music_TrackFiles")
                .WithColumn("AlbumId").AsInt32()
                .WithColumn("Path").AsString()
                .WithColumn("Size").AsInt64()
                .WithColumn("Modified").AsDateTimeOffset()
                .WithColumn("DateAdded").AsDateTimeOffset()
                .WithColumn("OriginalFilePath").AsString().Nullable()
                .WithColumn("SceneName").AsString().Nullable()
                .WithColumn("ReleaseGroup").AsString().Nullable()
                .WithColumn("Quality").AsString()
                .WithColumn("IndexerFlags").AsInt32().WithDefaultValue(0)
                .WithColumn("MediaInfo").AsString().Nullable();

            Create.Index("IX_Music_TrackFiles_AlbumId").OnTable("Music_TrackFiles").OnColumn("AlbumId").Ascending();
            Create.Index("IX_Music_TrackFiles_Path").OnTable("Music_TrackFiles").OnColumn("Path").Ascending();

            Create.TableForModel("Music_QualityDefinitions")
                .WithColumn("Quality").AsInt32().Unique()
                .WithColumn("Title").AsString().Unique()
                .WithColumn("MinSize").AsDouble().Nullable()
                .WithColumn("MaxSize").AsDouble().Nullable()
                .WithColumn("PreferredSize").AsDouble().Nullable();

            Create.TableForModel("Music_QualityProfiles")
                .WithColumn("Name").AsString().Unique()
                .WithColumn("UpgradeAllowed").AsBoolean().WithDefaultValue(false)
                .WithColumn("Cutoff").AsInt32()
                .WithColumn("Items").AsString()
                .WithColumn("MinFormatScore").AsInt32().WithDefaultValue(0)
                .WithColumn("CutoffFormatScore").AsInt32().WithDefaultValue(0)
                .WithColumn("FormatItems").AsString().WithDefaultValue("[]");

            Create.TableForModel("Music_MetadataProfiles")
                .WithColumn("Name").AsString().Unique()
                .WithColumn("PrimaryAlbumTypes").AsString()
                .WithColumn("SecondaryAlbumTypes").AsString()
                .WithColumn("ReleaseStatuses").AsString();

            Create.TableForModel("Music_RootFolders")
                .WithColumn("Name").AsString().Nullable()
                .WithColumn("Path").AsString().Unique()
                .WithColumn("DefaultMetadataProfileId").AsInt32().WithDefaultValue(0)
                .WithColumn("DefaultQualityProfileId").AsInt32().WithDefaultValue(0)
                .WithColumn("DefaultMonitorOption").AsInt32().WithDefaultValue(0)
                .WithColumn("DefaultNewItemMonitorOption").AsInt32().WithDefaultValue(0)
                .WithColumn("DefaultTags").AsString().WithDefaultValue("[]");

            Create.TableForModel("Music_NamingConfig")
                .WithColumn("RenameTracks").AsBoolean().WithDefaultValue(false)
                .WithColumn("ReplaceIllegalCharacters").AsBoolean().WithDefaultValue(true)
                .WithColumn("ColonReplacementFormat").AsInt32().WithDefaultValue(4)
                .WithColumn("StandardTrackFormat").AsString().Nullable()
                .WithColumn("MultiDiscTrackFormat").AsString().Nullable()
                .WithColumn("ArtistFolderFormat").AsString().Nullable();
        }
    }
}
