using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    // TV port, step 1 (docs/module-interface.md, "TV module: step 1 design"). Adds what the TV
    // module reads and writes. It upgrades a database in place and changes no existing row.
    //
    // Shared tables: the columns the TV module reads first.
    //   History.ItemId       the episode of a history row (ParentId is the series). Movie rows
    //                        keep 0: for a movie the parent and the item are the same entity.
    //   Blocklist.ItemIds    the episodes a blocklisted release covered, a JSON list.
    //   Blocklist.ReleaseType  single episode, multi-episode or season pack (0 is unknown).
    // DownloadHistory and PendingReleases need nothing. Sonarr's Blocklist.Source (what reported
    // the failure) is not carried: Radarr never stored it and the core failed-download event has
    // no such field.
    //
    // Module tables, all prefixed Tv_. Columns are Sonarr's property names so the importer maps
    // them one to one. A property that Sonarr stores as JSON is a string column here.
    //   Tv_Series, Tv_SeriesTranslations, Tv_Episodes, Tv_EpisodeFiles, Tv_SceneMappings,
    //   Tv_RootFolders, Tv_NamingConfig.
    // There is no season statistics table: Sonarr computes those from Episodes and EpisodeFiles.
    //
    // Deferred to the steps that need them: Tv_MetadataFiles, Tv_SubtitleFiles, Tv_ExtraFiles and
    // Tv_Metadata (import step), and the TV import list tables (import list decision).
    [Migration(4)]
    public class tv_module_tables : NzbDroneMigrationBase
    {
        // English in the global language catalog (Language.English). A migration keeps its own
        // copy of the value so it never changes with the code.
        private const int EnglishLanguageId = 14;

        protected override void MainDbUpgrade()
        {
            Alter.Table("History").AddColumn("ItemId").AsInt32().NotNullable().WithDefaultValue(0);
            Alter.Table("Blocklist").AddColumn("ItemIds").AsString().NotNullable().WithDefaultValue("[]");
            Alter.Table("Blocklist").AddColumn("ReleaseType").AsInt32().NotNullable().WithDefaultValue(0);

            Create.Index("IX_History_MediaKind_ItemId_Date").OnTable("History")
                .OnColumn("MediaKind").Ascending()
                .OnColumn("ItemId").Ascending()
                .OnColumn("Date").Descending();

            Create.TableForModel("Tv_Series")
                .WithColumn("TvdbId").AsInt32().Unique()
                .WithColumn("TvRageId").AsInt32().WithDefaultValue(0)
                .WithColumn("TvMazeId").AsInt32().WithDefaultValue(0)
                .WithColumn("ImdbId").AsString().Nullable()
                .WithColumn("TmdbId").AsInt32().WithDefaultValue(0)
                .WithColumn("MalIds").AsString().WithDefaultValue("[]")
                .WithColumn("AniListIds").AsString().WithDefaultValue("[]")
                .WithColumn("Title").AsString()
                .WithColumn("CleanTitle").AsString().Nullable()
                .WithColumn("SortTitle").AsString().Nullable()
                .WithColumn("Status").AsInt32()
                .WithColumn("Overview").AsString().Nullable()
                .WithColumn("AirTime").AsString().Nullable()
                .WithColumn("Monitored").AsBoolean()
                .WithColumn("MonitorNewItems").AsInt32().WithDefaultValue(0)
                .WithColumn("QualityProfileId").AsInt32()
                .WithColumn("SeasonFolder").AsBoolean()
                .WithColumn("LastInfoSync").AsDateTimeOffset().Nullable()
                .WithColumn("Runtime").AsInt32().WithDefaultValue(0)
                .WithColumn("Images").AsString().WithDefaultValue("[]")
                .WithColumn("SeriesType").AsInt32().WithDefaultValue(0)
                .WithColumn("Network").AsString().Nullable()
                .WithColumn("UseSceneNumbering").AsBoolean().WithDefaultValue(false)
                .WithColumn("TitleSlug").AsString().Nullable().Unique()
                .WithColumn("Path").AsString()
                .WithColumn("PendingPath").AsString().Nullable()
                .WithColumn("Year").AsInt32().WithDefaultValue(0)
                .WithColumn("Ratings").AsString().Nullable()
                .WithColumn("Genres").AsString().WithDefaultValue("[]")
                .WithColumn("Actors").AsString().WithDefaultValue("[]")
                .WithColumn("Certification").AsString().Nullable()
                .WithColumn("Added").AsDateTimeOffset()
                .WithColumn("FirstAired").AsDateTimeOffset().Nullable()
                .WithColumn("LastAired").AsDateTimeOffset().Nullable()
                .WithColumn("Language").AsInt32().WithDefaultValue(EnglishLanguageId)
                .WithColumn("OriginalLanguage").AsInt32().WithDefaultValue(EnglishLanguageId)
                .WithColumn("OriginalCountry").AsString().Nullable()
                .WithColumn("OriginalTitle").AsString().Nullable()
                .WithColumn("CleanOriginalTitle").AsString().Nullable()
                .WithColumn("SeasonType").AsString().Nullable().WithDefaultValue("official")
                .WithColumn("SeasonTypes").AsString().WithDefaultValue("[]")
                .WithColumn("Seasons").AsString().WithDefaultValue("[]")
                .WithColumn("Tags").AsString().WithDefaultValue("[]")
                .WithColumn("AddOptions").AsString().Nullable();

            Create.Index("IX_Tv_Series_Path").OnTable("Tv_Series").OnColumn("Path").Ascending();

            Create.Index("IX_Tv_Series_CleanTitle").OnTable("Tv_Series").OnColumn("CleanTitle").Ascending();

            Create.Index("IX_Tv_Series_CleanOriginalTitle").OnTable("Tv_Series").OnColumn("CleanOriginalTitle").Ascending();

            Create.Index("IX_Tv_Series_TmdbId").OnTable("Tv_Series").OnColumn("TmdbId").Ascending();

            Create.TableForModel("Tv_SeriesTranslations")
                .WithColumn("SeriesId").AsInt32()
                .WithColumn("Title").AsString().Nullable()
                .WithColumn("CleanTitle").AsString().Nullable()
                .WithColumn("Overview").AsString().Nullable()
                .WithColumn("Language").AsInt32();

            Create.Index("IX_Tv_SeriesTranslations_SeriesId").OnTable("Tv_SeriesTranslations").OnColumn("SeriesId").Ascending();

            Create.TableForModel("Tv_Episodes")
                .WithColumn("SeriesId").AsInt32()
                .WithColumn("TvdbId").AsInt32().WithDefaultValue(0)
                .WithColumn("EpisodeFileId").AsInt32().WithDefaultValue(0)
                .WithColumn("SeasonNumber").AsInt32()
                .WithColumn("EpisodeNumber").AsInt32()
                .WithColumn("Title").AsString().Nullable()
                .WithColumn("AirDate").AsString().Nullable()
                .WithColumn("AirDateUtc").AsDateTimeOffset().Nullable()
                .WithColumn("Overview").AsString().Nullable()
                .WithColumn("Monitored").AsBoolean()
                .WithColumn("AbsoluteEpisodeNumber").AsInt32().Nullable()
                .WithColumn("SceneAbsoluteEpisodeNumber").AsInt32().Nullable()
                .WithColumn("SceneSeasonNumber").AsInt32().Nullable()
                .WithColumn("SceneEpisodeNumber").AsInt32().Nullable()
                .WithColumn("AiredAfterSeasonNumber").AsInt32().Nullable()
                .WithColumn("AiredBeforeSeasonNumber").AsInt32().Nullable()
                .WithColumn("AiredBeforeEpisodeNumber").AsInt32().Nullable()
                .WithColumn("UnverifiedSceneNumbering").AsBoolean().WithDefaultValue(false)
                .WithColumn("Ratings").AsString().Nullable()
                .WithColumn("Images").AsString().WithDefaultValue("[]")
                .WithColumn("LastSearchTime").AsDateTimeOffset().Nullable()
                .WithColumn("Runtime").AsInt32().WithDefaultValue(0)
                .WithColumn("FinaleType").AsString().Nullable();

            Create.Index("IX_Tv_Episodes_SeriesId").OnTable("Tv_Episodes").OnColumn("SeriesId").Ascending();

            Create.Index("IX_Tv_Episodes_EpisodeFileId").OnTable("Tv_Episodes").OnColumn("EpisodeFileId").Ascending();

            Create.Index("IX_Tv_Episodes_SeriesId_SeasonNumber_EpisodeNumber").OnTable("Tv_Episodes")
                .OnColumn("SeriesId").Ascending()
                .OnColumn("SeasonNumber").Ascending()
                .OnColumn("EpisodeNumber").Ascending();

            Create.Index("IX_Tv_Episodes_SeriesId_AirDate").OnTable("Tv_Episodes")
                .OnColumn("SeriesId").Ascending()
                .OnColumn("AirDate").Ascending();

            Create.TableForModel("Tv_EpisodeFiles")
                .WithColumn("SeriesId").AsInt32()
                .WithColumn("SeasonNumber").AsInt32()
                .WithColumn("RelativePath").AsString().Nullable()
                .WithColumn("Size").AsInt64()
                .WithColumn("DateAdded").AsDateTimeOffset()
                .WithColumn("OriginalFilePath").AsString().Nullable()
                .WithColumn("SceneName").AsString().Nullable()
                .WithColumn("ReleaseGroup").AsString().Nullable()
                .WithColumn("ReleaseHash").AsString().Nullable()
                .WithColumn("Quality").AsString()
                .WithColumn("IndexerFlags").AsInt32().WithDefaultValue(0)
                .WithColumn("MediaInfo").AsString().Nullable()
                .WithColumn("Languages").AsString().WithDefaultValue("[]")
                .WithColumn("ReleaseType").AsInt32().WithDefaultValue(0);

            Create.Index("IX_Tv_EpisodeFiles_SeriesId").OnTable("Tv_EpisodeFiles").OnColumn("SeriesId").Ascending();

            Create.TableForModel("Tv_SceneMappings")
                .WithColumn("MappingId").AsString().Nullable()
                .WithColumn("Title").AsString().Nullable()
                .WithColumn("ParseTerm").AsString().Nullable()
                .WithColumn("SearchTerm").AsString().Nullable()
                .WithColumn("TvdbId").AsInt32()
                .WithColumn("SeasonNumber").AsInt32().Nullable()
                .WithColumn("SceneSeasonNumber").AsInt32().Nullable()
                .WithColumn("SceneOrigin").AsString().Nullable()
                .WithColumn("SearchMode").AsInt32().Nullable()
                .WithColumn("Comment").AsString().Nullable()
                .WithColumn("FilterRegex").AsString().Nullable()
                .WithColumn("Type").AsString().Nullable();

            Create.TableForModel("Tv_RootFolders")
                .WithColumn("Path").AsString().Unique();

            Create.TableForModel("Tv_NamingConfig")
                .WithColumn("RenameEpisodes").AsBoolean().WithDefaultValue(false)
                .WithColumn("ReplaceIllegalCharacters").AsBoolean().WithDefaultValue(true)
                .WithColumn("ColonReplacementFormat").AsInt32().WithDefaultValue(4)
                .WithColumn("CustomColonReplacementFormat").AsString().Nullable()
                .WithColumn("MultiEpisodeStyle").AsInt32().WithDefaultValue(5)
                .WithColumn("StandardEpisodeFormat").AsString().Nullable()
                .WithColumn("DailyEpisodeFormat").AsString().Nullable()
                .WithColumn("AnimeEpisodeFormat").AsString().Nullable()
                .WithColumn("SeriesFolderFormat").AsString().Nullable()
                .WithColumn("SeasonFolderFormat").AsString().Nullable()
                .WithColumn("SpecialsFolderFormat").AsString().Nullable();
        }
    }
}
