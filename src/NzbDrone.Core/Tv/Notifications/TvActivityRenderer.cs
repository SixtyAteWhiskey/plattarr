using System.Collections.Generic;
using System.IO;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv.MediaFiles;
using NzbDrone.Core.Tv.MediaFiles.MediaInfo;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Core.Tv.RootFolders;

namespace NzbDrone.Core.Tv.Notifications
{
    // Contract 7. Turns a series, its episodes, a file and a release into the neutral values notification
    // providers show. The item label is Sonarr's own title text for the episodes ("1x01 - Pilot", a
    // daily show's air date), so a provider that appends it reads like Sonarr's.
    public class TvActivityRenderer : IActivityRenderer
    {
        private readonly ISeriesService _seriesService;
        private readonly IEpisodeService _episodeService;
        private readonly IRootFolderService _rootFolderService;

        public TvActivityRenderer(ISeriesService seriesService,
                                  IEpisodeService episodeService,
                                  IRootFolderService rootFolderService)
        {
            _seriesService = seriesService;
            _episodeService = episodeService;
            _rootFolderService = rootFolderService;
        }

        public MediaKind Kind => MediaKind.Tv;

        public ActivitySubject Describe(int parentId, IReadOnlyList<int> itemIds)
        {
            var series = _seriesService.GetSeries(parentId);
            var episodes = itemIds == null || itemIds.Count == 0
                ? new List<Episode>()
                : _episodeService.GetEpisodes(itemIds);

            return Describe(series, episodes);
        }

        public static string DescribeEpisodes(Series series, IReadOnlyList<Episode> episodes)
        {
            if (episodes == null || episodes.Count == 0)
            {
                return string.Empty;
            }

            if (series.SeriesType == SeriesTypes.Daily)
            {
                var episode = episodes[0];

                return $"{episode.AirDate} - {episode.Title}";
            }

            var episodeNumbers = string.Concat(episodes.Select(e => $"x{e.EpisodeNumber:00}"));
            var episodeTitles = string.Join(" + ", episodes.Select(e => e.Title));

            return $"{episodes[0].SeasonNumber}{episodeNumbers} - {episodeTitles}";
        }

        public ActivitySubject Describe(Series series, IReadOnlyList<Episode> episodes = null)
        {
            if (series == null)
            {
                return null;
            }

            var subject = new ActivitySubject
            {
                Kind = MediaKind.Tv,
                KindLabel = "TV Show",
                ItemKindLabel = "Episode",
                OmitYearFromTitle = true,
                ParentId = series.Id,
                ParentTitle = series.Title,
                ItemLabel = DescribeEpisodes(series, episodes),
                ParentPath = series.Path,
                Year = series.Year,
                Overview = series.Overview,
                Genres = series.Genres ?? new List<string>(),
                Rating = series.Ratings?.Value,
                PosterUrl = series.Images?.FirstOrDefault(x => x.CoverType == MediaCoverTypes.Poster)?.RemoteUrl,
                FanartUrl = series.Images?.FirstOrDefault(x => x.CoverType == MediaCoverTypes.Fanart)?.RemoteUrl,
                Tags = series.Tags ?? new HashSet<int>()
            };

            subject.ResolveRootFolderWith(() => series.Path.IsNotNullOrWhiteSpace() ? _rootFolderService.GetBestRootFolderPath(series.Path) : null);

            if (series.TvdbId > 0)
            {
                var tvdb = new NotificationMetadataLink(MetadataLinkType.Tvdb, "TVDb", $"https://www.thetvdb.com/?tab=series&id={series.TvdbId}");

                subject.ExternalIds[ExternalIdTypes.Tvdb] = series.TvdbId.ToString();
                subject.Links.Add(tvdb);
                subject.MetadataUrl = tvdb.Link;
            }

            if (series.TmdbId > 0)
            {
                subject.ExternalIds[ExternalIdTypes.Tmdb] = series.TmdbId.ToString();
                subject.Links.Add(new NotificationMetadataLink(MetadataLinkType.Tmdb, "TMDb", $"https://www.themoviedb.org/tv/{series.TmdbId}"));
            }

            if (series.TvdbId > 0)
            {
                subject.Links.Add(new NotificationMetadataLink(MetadataLinkType.Trakt, "Trakt", $"https://trakt.tv/search/tvdb/{series.TvdbId}?id_type=show"));
            }

            if (series.TvMazeId > 0)
            {
                subject.ExternalIds[ExternalIdTypes.TvMaze] = series.TvMazeId.ToString();
                subject.Links.Add(new NotificationMetadataLink(MetadataLinkType.Tvmaze, "TVMaze", $"https://www.tvmaze.com/shows/{series.TvMazeId}/_"));
            }

            if (series.ImdbId.IsNotNullOrWhiteSpace())
            {
                subject.ExternalIds[ExternalIdTypes.Imdb] = series.ImdbId;
                subject.Links.Add(new NotificationMetadataLink(MetadataLinkType.Imdb, "IMDb", $"https://www.imdb.com/title/{series.ImdbId}"));
            }

            return subject;
        }

        public NotificationFile DescribeFile(Series series, EpisodeFile episodeFile)
        {
            if (episodeFile == null)
            {
                return null;
            }

            var mediaInfo = episodeFile.MediaInfo;

            return new NotificationFile
            {
                Id = episodeFile.Id,
                Path = Path.Combine(series.Path, episodeFile.RelativePath),
                RelativePath = episodeFile.RelativePath,
                Size = episodeFile.Size,
                DateAdded = episodeFile.DateAdded,
                SceneName = episodeFile.SceneName,
                ReleaseGroup = episodeFile.ReleaseGroup,
                QualityName = episodeFile.Quality?.Quality?.Name,
                MediaInfo = mediaInfo == null ? null : new NotificationMediaInfo
                {
                    VideoCodec = MediaInfoFormatter.FormatVideoCodec(mediaInfo, null),
                    AudioCodec = MediaInfoFormatter.FormatAudioCodec(mediaInfo.PrimaryAudioStream, null),
                    AudioChannels = MediaInfoFormatter.FormatAudioChannels(mediaInfo.PrimaryAudioStream),
                    AudioLanguages = mediaInfo.AudioStreams?.Select(l => l.Language).Distinct().ToList() ?? new List<string>(),
                    Subtitles = mediaInfo.SubtitleStreams?.Select(l => l.Language).Distinct().ToList() ?? new List<string>()
                }
            };
        }

        public NotificationRelease DescribeRelease(RemoteEpisode remoteEpisode, QualityModel quality)
        {
            return new NotificationRelease
            {
                Title = remoteEpisode.Release.Title,
                Indexer = remoteEpisode.Release.Indexer,
                Size = remoteEpisode.Release.Size.GetValueOrDefault(),
                ReleaseGroup = remoteEpisode.ParsedEpisodeInfo.ReleaseGroup,
                QualityName = quality.Quality.Name,
                CustomFormats = remoteEpisode.CustomFormats.Select(c => c.Name).ToList(),
                CustomFormatScore = remoteEpisode.CustomFormatScore
            };
        }

        public NotificationRelease DescribeRelease(GrabbedReleaseInfo release)
        {
            if (release == null)
            {
                return null;
            }

            return new NotificationRelease
            {
                Title = release.Title,
                Indexer = release.Indexer,
                Size = release.Size
            };
        }
    }
}
