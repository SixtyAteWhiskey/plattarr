using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Tv.MediaFiles;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Tv.Upstream
{
    // Sonarr's series (with their seasons), episode files and episodes (docs/importer.md,
    // "From sonarr.db"). Seasons are a JSON document on the series, so a season's monitored flag
    // travels with it; each episode keeps its own monitored flag, so a first search after the
    // import grabs exactly what Sonarr would have (an unmonitored special stays unmonitored).
    //
    // An episode points at its file (Episodes.EpisodeFileId) and a file at its series only, so the
    // order is series, files, episodes, and every link is known when its row is written.
    //
    // The target has no foreign keys and the series list indexes the quality profile by ID, so a
    // series that refers to a profile that was not imported, or that has no path, is skipped as
    // CORRUPT (one row, named in the report), and so are its files and episodes. An episode whose
    // file was skipped is imported without a file; the next disk scan finds the file again.
    public class SeriesStage : IImportStage
    {
        public const string SeriesTable = "Series";
        public const string EpisodesTable = "Episodes";
        public const string EpisodeFilesTable = "EpisodeFiles";

        private readonly ISeriesRepository _series;
        private readonly IEpisodeRepository _episodes;
        private readonly IMediaFileRepository _files;
        private readonly IEnumerable<IUpstreamCatalog> _catalogs;

        public SeriesStage(ISeriesRepository series, IEpisodeRepository episodes, IMediaFileRepository files, IEnumerable<IUpstreamCatalog> catalogs)
        {
            _series = series;
            _episodes = episodes;
            _files = files;
            _catalogs = catalogs;
        }

        public string Name => "Series, episodes and episode files";

        public int Order => ImportOrder.Series;

        public IReadOnlyList<SourceTableRef> Tables { get; } = new[]
        {
            new SourceTableRef(UpstreamApps.Sonarr, SeriesTable),
            new SourceTableRef(UpstreamApps.Sonarr, EpisodeFilesTable),
            new SourceTableRef(UpstreamApps.Sonarr, EpisodesTable)
        };

        public void Run(ImportSession session)
        {
            var snapshot = session.Snapshot(UpstreamApps.Sonarr);

            if (snapshot == null)
            {
                return;
            }

            var catalog = _catalogs.For(UpstreamApps.Sonarr);
            var titles = ImportSeries(session, snapshot, catalog);

            ImportFiles(session, snapshot, catalog);
            ImportEpisodes(session, snapshot, titles);
        }

        // Returns the title of every series by its Sonarr ID, to name skipped episodes.
        private Dictionary<long, string> ImportSeries(ImportSession session, SourceSnapshot snapshot, IUpstreamCatalog catalog)
        {
            var titles = new Dictionary<long, string>();
            var batch = new RowBatch<Series>();

            foreach (var row in snapshot.Rows(SeriesTable))
            {
                var id = row.GetLong("Id");
                var title = row.GetString("Title");

                titles[id] = title;

                // Sonarr's column is nullable; a series without a profile cannot be searched.
                var profileSourceId = row.GetNullableLong("QualityProfileId");

                if (!profileSourceId.HasValue || !session.IdMap.TryResolve(UpstreamApps.Sonarr, ProfilesStage.QualityProfilesTable, profileSourceId.Value, out var profileId))
                {
                    session.SkipCorrupt(UpstreamApps.Sonarr, SeriesTable, id, title, "Refers to a quality profile that was not imported");
                    continue;
                }

                try
                {
                    var path = row.GetString("Path");

                    if (string.IsNullOrWhiteSpace(path))
                    {
                        throw new CorruptSourceDataException("Series has no path");
                    }

                    var tags = TagRemapper.Remap(UpstreamApps.Sonarr, row.GetJson("Tags"), session.IdMap, out var dropped);

                    var series = new Series
                    {
                        TvdbId = row.GetInt("TvdbId"),
                        TvRageId = row.GetNullableInt("TvRageId") ?? 0,
                        TvMazeId = row.GetNullableInt("TvMazeId") ?? 0,
                        ImdbId = row.GetString("ImdbId"),
                        TmdbId = row.GetNullableInt("TmdbId") ?? 0,
                        MalIds = TvDocuments.ReadIntSet(row, "MalIds"),
                        AniListIds = TvDocuments.ReadIntSet(row, "AniListIds"),
                        Title = title,
                        CleanTitle = row.GetString("CleanTitle"),
                        SortTitle = row.GetString("SortTitle"),
                        Status = (SeriesStatusType)row.GetInt("Status"),
                        Overview = row.GetString("Overview"),
                        AirTime = row.GetString("AirTime"),
                        Monitored = row.GetBool("Monitored"),
                        MonitorNewItems = (NewItemMonitorTypes)(row.GetNullableInt("MonitorNewItems") ?? 0),
                        QualityProfileId = (int)profileId,
                        SeasonFolder = row.GetBool("SeasonFolder"),
                        LastInfoSync = row.GetNullableDateTime("LastInfoSync"),
                        Runtime = row.GetNullableInt("Runtime") ?? 0,
                        Images = TvDocuments.ReadImages(row, "Images"),
                        SeriesType = (SeriesTypes)row.GetInt("SeriesType"),
                        Network = row.GetString("Network"),
                        UseSceneNumbering = row.GetBool("UseSceneNumbering"),
                        TitleSlug = row.GetString("TitleSlug"),
                        Path = session.Paths.Map(path),
                        Year = row.GetNullableInt("Year") ?? 0,
                        Ratings = TvDocuments.ReadRatings(row, "Ratings"),
                        Genres = TvDocuments.ReadStrings(row, "Genres"),
                        Actors = TvDocuments.ReadActors(row, "Actors"),
                        Certification = row.GetString("Certification"),
                        Added = row.GetNullableDateTime("Added") ?? session.ImportedAt,
                        FirstAired = row.GetNullableDateTime("FirstAired"),
                        LastAired = row.GetNullableDateTime("LastAired"),

                        // Sonarr 4 looks metadata up in English and keeps TVDB's aired order;
                        // Plattarr stores both choices on the series.
                        Language = Language.English,
                        SeasonType = SeasonType.Official,
                        OriginalLanguage = OriginalLanguage(row, catalog),
                        Seasons = TvDocuments.ReadSeasons(row, "Seasons"),
                        Tags = new HashSet<int>(tags),
                        AddOptions = AddOptions(session, row, id, title)
                    };

                    if (dropped > 0)
                    {
                        session.Warn($"Series \"{title}\" (Sonarr {id}): dropped {dropped} tag(s) that were not imported");
                    }

                    batch.Add(id, series);
                }
                catch (Exception ex) when (SourceDocuments.IsBad(ex))
                {
                    session.SkipCorrupt(UpstreamApps.Sonarr, SeriesTable, id, title, "Series could not be read: " + SourceDocuments.Describe(ex));
                }
            }

            batch.Write(session, UpstreamApps.Sonarr, SeriesTable, _series);

            return titles;
        }

        private static Language OriginalLanguage(SourceRow row, IUpstreamCatalog catalog)
        {
            var language = row.GetNullableInt("OriginalLanguage");

            return language.HasValue ? (Language)CatalogRemapper.LanguageId(language.Value, catalog) : Language.English;
        }

        // Options left on a series whose add did not finish; Sonarr clears them afterwards. One the
        // target cannot read is dropped with a warning rather than losing the series over it.
        private static AddSeriesOptions AddOptions(ImportSession session, SourceRow row, long id, string title)
        {
            try
            {
                return TvDocuments.ReadAddOptions(row, "AddOptions");
            }
            catch (Exception ex) when (SourceDocuments.IsBad(ex))
            {
                session.Warn($"Series \"{title}\" (Sonarr {id}): its unfinished add options could not be read and were dropped ({SourceDocuments.Describe(ex)})");
                return null;
            }
        }

        private void ImportFiles(ImportSession session, SourceSnapshot snapshot, IUpstreamCatalog catalog)
        {
            var batch = new RowBatch<EpisodeFile>();
            var mediaInfoDropped = 0;

            foreach (var row in snapshot.Rows(EpisodeFilesTable))
            {
                var id = row.GetLong("Id");
                var relativePath = row.GetString("RelativePath");

                if (!session.IdMap.TryResolve(UpstreamApps.Sonarr, SeriesTable, row.GetLong("SeriesId"), out var seriesId))
                {
                    session.SkipCorrupt(UpstreamApps.Sonarr, EpisodeFilesTable, id, relativePath, "Refers to a series that was not imported");
                    continue;
                }

                try
                {
                    var file = new EpisodeFile
                    {
                        SeriesId = (int)seriesId,
                        SeasonNumber = row.GetInt("SeasonNumber"),
                        RelativePath = relativePath,
                        Size = row.GetLong("Size"),
                        DateAdded = row.GetDateTime("DateAdded"),
                        OriginalFilePath = row.GetString("OriginalFilePath"),
                        SceneName = row.GetString("SceneName"),
                        ReleaseGroup = row.GetString("ReleaseGroup"),
                        ReleaseHash = row.GetString("ReleaseHash"),
                        Quality = TvDocuments.ReadQuality(row, "Quality", catalog),

                        // Sonarr's flag bits differ from the global (Radarr) ones from bit 8 up (F4).
                        IndexerFlags = ProfilesStage.SonarrFlags(row.GetNullableInt("IndexerFlags") ?? 0),
                        Languages = TvDocuments.ReadLanguages(row, "Languages", catalog),
                        ReleaseType = (ReleaseType)(row.GetNullableInt("ReleaseType") ?? 0)
                    };

                    // Media info is a cache of the file's own streams: Plattarr reads it again for
                    // any file without it (UpdateMediaInfoService), so an unreadable document costs
                    // a rescan, not the file.
                    try
                    {
                        file.MediaInfo = TvDocuments.ReadMediaInfo(row, "MediaInfo");
                    }
                    catch (Exception ex) when (SourceDocuments.IsBad(ex))
                    {
                        mediaInfoDropped++;
                    }

                    batch.Add(id, file);
                }
                catch (Exception ex) when (SourceDocuments.IsBad(ex))
                {
                    session.SkipCorrupt(UpstreamApps.Sonarr, EpisodeFilesTable, id, relativePath, "Episode file could not be read: " + SourceDocuments.Describe(ex));
                }
            }

            batch.Write(session, UpstreamApps.Sonarr, EpisodeFilesTable, _files);

            if (mediaInfoDropped > 0)
            {
                session.Warn($"{mediaInfoDropped} episode file(s) had media info Plattarr could not read; it was dropped and is read again from the files on the next disk scan");
            }
        }

        private void ImportEpisodes(ImportSession session, SourceSnapshot snapshot, Dictionary<long, string> titles)
        {
            var batch = new RowBatch<Episode>();
            var sourceFileIds = snapshot.Rows(EpisodeFilesTable).Select(r => r.GetLong("Id")).ToHashSet();
            var missingFiles = 0;

            foreach (var row in snapshot.Rows(EpisodesTable))
            {
                var id = row.GetLong("Id");
                var seriesSourceId = row.GetLong("SeriesId");
                var seasonNumber = row.GetInt("SeasonNumber");
                var episodeNumber = row.GetInt("EpisodeNumber");

                titles.TryGetValue(seriesSourceId, out var seriesTitle);

                var name = $"{seriesTitle} S{seasonNumber:00}E{episodeNumber:00}";

                if (!session.IdMap.TryResolve(UpstreamApps.Sonarr, SeriesTable, seriesSourceId, out var seriesId))
                {
                    session.SkipCorrupt(UpstreamApps.Sonarr, EpisodesTable, id, name, "Refers to a series that was not imported");
                    continue;
                }

                try
                {
                    var fileSourceId = row.GetNullableLong("EpisodeFileId") ?? 0;
                    var fileId = 0L;

                    if (fileSourceId > 0 && !session.IdMap.TryResolve(UpstreamApps.Sonarr, EpisodeFilesTable, fileSourceId, out fileId) && !sourceFileIds.Contains(fileSourceId))
                    {
                        // A file that was skipped is already in the report; one that never existed is not.
                        missingFiles++;
                    }

                    batch.Add(id, new Episode
                    {
                        SeriesId = (int)seriesId,
                        TvdbId = row.GetNullableInt("TvdbId") ?? 0,
                        EpisodeFileId = (int)fileId,
                        SeasonNumber = seasonNumber,
                        EpisodeNumber = episodeNumber,
                        Title = row.GetString("Title"),
                        AirDate = row.GetString("AirDate"),
                        AirDateUtc = row.GetNullableDateTime("AirDateUtc"),
                        Overview = row.GetString("Overview"),

                        // Nullable in Sonarr's schema; Sonarr reads NULL as not monitored.
                        Monitored = (row.GetNullableLong("Monitored") ?? 0) != 0,
                        AbsoluteEpisodeNumber = row.GetNullableInt("AbsoluteEpisodeNumber"),
                        SceneAbsoluteEpisodeNumber = row.GetNullableInt("SceneAbsoluteEpisodeNumber"),
                        SceneSeasonNumber = row.GetNullableInt("SceneSeasonNumber"),
                        SceneEpisodeNumber = row.GetNullableInt("SceneEpisodeNumber"),
                        AiredAfterSeasonNumber = row.GetNullableInt("AiredAfterSeasonNumber"),
                        AiredBeforeSeasonNumber = row.GetNullableInt("AiredBeforeSeasonNumber"),
                        AiredBeforeEpisodeNumber = row.GetNullableInt("AiredBeforeEpisodeNumber"),
                        UnverifiedSceneNumbering = (row.GetNullableLong("UnverifiedSceneNumbering") ?? 0) != 0,
                        Ratings = TvDocuments.ReadRatings(row, "Ratings"),
                        Images = TvDocuments.ReadImages(row, "Images"),
                        LastSearchTime = row.GetNullableDateTime("LastSearchTime"),
                        Runtime = row.GetNullableInt("Runtime") ?? 0,
                        FinaleType = row.GetString("FinaleType")
                    });
                }
                catch (Exception ex) when (SourceDocuments.IsBad(ex))
                {
                    session.SkipCorrupt(UpstreamApps.Sonarr, EpisodesTable, id, name, "Episode could not be read: " + SourceDocuments.Describe(ex));
                }
            }

            batch.Write(session, UpstreamApps.Sonarr, EpisodesTable, _episodes);

            if (missingFiles > 0)
            {
                session.Warn($"{missingFiles} episode(s) pointed at a file that is not in Sonarr's database; they were imported without a file");
            }
        }
    }
}
