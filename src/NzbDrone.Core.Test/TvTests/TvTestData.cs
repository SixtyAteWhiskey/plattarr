using System;
using System.Collections.Generic;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.MediaFiles;
using NzbDrone.Core.Tv.MediaFiles.MediaInfo;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Test.TvTests
{
    // Fully populated TV entities, so a round trip through the database proves every column.
    internal static class TvTestData
    {
        public static Series NewSeries(int tvdbId = 81189, string title = "Breaking Bad", int qualityProfileId = 1)
        {
            var slug = title.ToLowerInvariant().Replace(' ', '-');

            return new Series
            {
                TvdbId = tvdbId,
                TvRageId = 18164,
                TvMazeId = 169,
                ImdbId = $"tt{tvdbId:D7}",
                TmdbId = 1396,
                MalIds = new HashSet<int> { 11, 12 },
                AniListIds = new HashSet<int> { 21 },
                Title = title,
                CleanTitle = slug.Replace("-", string.Empty),
                SortTitle = title.ToLowerInvariant(),
                Status = SeriesStatusType.Ended,
                Overview = "A chemistry teacher turned manufacturer.",
                AirTime = "21:00",
                Monitored = true,
                MonitorNewItems = NewItemMonitorTypes.None,
                QualityProfileId = qualityProfileId,
                SeasonFolder = true,
                LastInfoSync = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
                Runtime = 47,
                Images = new List<MediaCover.MediaCover>
                {
                    new MediaCover.MediaCover(MediaCoverTypes.Poster, "https://img.example/poster.jpg"),
                    new MediaCover.MediaCover(MediaCoverTypes.Banner, "https://img.example/banner.jpg")
                },
                SeriesType = SeriesTypes.Standard,
                Network = "AMC",
                UseSceneNumbering = true,
                TitleSlug = slug,
                Path = "/data/tv/" + title,
                PendingPath = "/data/tv/pending/" + title,
                Year = 2008,
                Ratings = new Ratings { Votes = 1200, Value = 9.5m },
                Genres = new List<string> { "Crime", "Drama" },
                Actors = new List<Actor>
                {
                    new Actor { Name = "Bryan Cranston", Character = "Walter White", Images = new List<MediaCover.MediaCover> { new MediaCover.MediaCover(MediaCoverTypes.Headshot, "https://img.example/bc.jpg") } }
                },
                Certification = "TV-MA",
                Added = new DateTime(2026, 9, 1, 8, 30, 0, DateTimeKind.Utc),
                FirstAired = new DateTime(2008, 1, 20, 0, 0, 0, DateTimeKind.Utc),
                LastAired = new DateTime(2013, 9, 29, 0, 0, 0, DateTimeKind.Utc),
                Language = Language.English,
                OriginalLanguage = Language.French,
                OriginalCountry = "us",
                OriginalTitle = "Breaking Bad Original",
                CleanOriginalTitle = "breakingbadoriginal",
                SeasonType = "official",
                SeasonTypes = new List<SeasonType>
                {
                    new SeasonType { Name = "Aired Order", Type = "official", SeasonNumbers = new List<int> { 1, 2 }, EpisodeCount = 20 }
                },
                Seasons = new List<Season>
                {
                    new Season { SeasonNumber = 1, Monitored = true, Title = "Season 1", Images = new List<MediaCover.MediaCover> { new MediaCover.MediaCover(MediaCoverTypes.Poster, "https://img.example/s1.jpg") } },
                    new Season { SeasonNumber = 2, Monitored = false }
                },
                Tags = new HashSet<int> { 3, 5 },
                AddOptions = new AddSeriesOptions { Monitor = MonitorTypes.Future, SearchForMissingEpisodes = true, IgnoreEpisodesWithFiles = true }
            };
        }

        public static Episode NewEpisode(int seriesId, int season, int number, DateTime? airDateUtc = null, bool monitored = true, int episodeFileId = 0)
        {
            return new Episode
            {
                SeriesId = seriesId,
                TvdbId = (season * 1000) + number,
                EpisodeFileId = episodeFileId,
                SeasonNumber = season,
                EpisodeNumber = number,
                Title = $"Episode {season}x{number}",
                AirDate = airDateUtc?.ToString(Episode.AIR_DATE_FORMAT),
                AirDateUtc = airDateUtc,
                Overview = "Overview",
                Monitored = monitored,
                AbsoluteEpisodeNumber = number,
                SceneAbsoluteEpisodeNumber = number + 100,
                SceneSeasonNumber = season + 10,
                SceneEpisodeNumber = number + 20,
                AiredAfterSeasonNumber = null,
                AiredBeforeSeasonNumber = season,
                AiredBeforeEpisodeNumber = number,
                UnverifiedSceneNumbering = true,
                Ratings = new Ratings { Votes = 10, Value = 8.1m },
                Images = new List<MediaCover.MediaCover> { new MediaCover.MediaCover(MediaCoverTypes.Screenshot, "https://img.example/e.jpg") },
                LastSearchTime = new DateTime(2026, 10, 2, 3, 4, 5, DateTimeKind.Utc),
                Runtime = 47,
                FinaleType = "season"
            };
        }

        public static EpisodeFile NewEpisodeFile(int seriesId, int season, string relativePath, Quality quality = null)
        {
            return new EpisodeFile
            {
                SeriesId = seriesId,
                SeasonNumber = season,
                RelativePath = relativePath,
                Size = 1_234_567_890,
                DateAdded = new DateTime(2026, 10, 3, 4, 5, 6, DateTimeKind.Utc),
                OriginalFilePath = "downloads/" + relativePath,
                SceneName = "Show.S01E01.1080p.WEB-DL-GRP",
                ReleaseGroup = "GRP",
                ReleaseHash = "ABCD1234",
                Quality = new QualityModel(quality ?? Quality.Bluray1080p, new Revision(2)),
                IndexerFlags = IndexerFlags.G_Freeleech,
                MediaInfo = new MediaInfoModel
                {
                    ContainerFormat = "Matroska",
                    VideoFormat = "h264",
                    VideoBitDepth = 10,
                    VideoHdrFormat = HdrFormat.Hdr10,
                    Height = 1080,
                    Width = 1920,
                    RunTime = TimeSpan.FromMinutes(47),
                    VideoFps = 23.976m,
                    AudioStreams = new List<MediaInfoAudioStreamModel>
                    {
                        new MediaInfoAudioStreamModel { Language = "eng", Format = "eac3", Channels = 6, ChannelPositions = "5.1", Bitrate = 640000 },
                        new MediaInfoAudioStreamModel { Language = "fra", Format = "ac3", Channels = 2, ChannelPositions = "2.0", Bitrate = 192000 }
                    },
                    SubtitleStreams = new List<MediaInfoSubtitleStreamModel>
                    {
                        new MediaInfoSubtitleStreamModel { Language = "eng", Format = "subrip", Forced = false, HearingImpaired = true }
                    },
                    ScanType = "Progressive"
                },
                Languages = new List<Language> { Language.English, Language.French },
                ReleaseType = ReleaseType.SeasonPack
            };
        }
    }
}
