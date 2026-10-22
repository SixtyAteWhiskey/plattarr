using System.Collections.Generic;
using System.IO;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.MediaCover;
using NzbDrone.Core.Movies.MediaFiles;
using NzbDrone.Core.Movies.MediaFiles.MediaInfo;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Movies.RootFolders;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Movies.Notifications
{
    // Contract 7. Turns a movie, its file and its release into the neutral values notification
    // providers show. Movies has no separate items, so ItemLabel stays empty.
    public class MovieActivityRenderer : IActivityRenderer
    {
        private readonly IMovieService _movieService;
        private readonly IRootFolderService _rootFolderService;

        public MovieActivityRenderer(IMovieService movieService, IRootFolderService rootFolderService)
        {
            _movieService = movieService;
            _rootFolderService = rootFolderService;
        }

        public MediaKind Kind => MediaKind.Movies;

        public ActivitySubject Describe(int parentId, IReadOnlyList<int> itemIds)
        {
            return Describe(_movieService.GetMovie(parentId));
        }

        public ActivitySubject Describe(Movie movie)
        {
            if (movie == null)
            {
                return null;
            }

            var metadata = movie.MovieMetadata.Value;

            var subject = new ActivitySubject
            {
                Kind = MediaKind.Movies,
                KindLabel = "Movie",
                ParentId = movie.Id,
                ParentTitle = metadata.Title,
                ItemLabel = string.Empty,
                ParentPath = movie.Path,
                Year = metadata.Year,
                Overview = metadata.Overview,
                Genres = metadata.Genres ?? new List<string>(),
                Rating = metadata.Ratings?.Tmdb?.Value,
                PosterUrl = metadata.Images?.FirstOrDefault(x => x.CoverType == MediaCoverTypes.Poster)?.RemoteUrl,
                FanartUrl = metadata.Images?.FirstOrDefault(x => x.CoverType == MediaCoverTypes.Fanart)?.RemoteUrl,
                Tags = movie.Tags ?? new HashSet<int>()
            };

            subject.ResolveRootFolderWith(() => movie.Path.IsNotNullOrWhiteSpace() ? _rootFolderService.GetBestRootFolderPath(movie.Path) : null);

            if (metadata.TmdbId > 0)
            {
                subject.ExternalIds[ExternalIdTypes.Tmdb] = metadata.TmdbId.ToString();
                subject.Links.Add(new NotificationMetadataLink(MetadataLinkType.Tmdb, "TMDb", $"https://www.themoviedb.org/movie/{metadata.TmdbId}"));
            }

            if (metadata.ImdbId.IsNotNullOrWhiteSpace())
            {
                subject.ExternalIds[ExternalIdTypes.Imdb] = metadata.ImdbId;
                subject.Links.Add(new NotificationMetadataLink(MetadataLinkType.Trakt, "Trakt", $"https://trakt.tv/movies/{metadata.ImdbId}"));
                subject.Links.Add(new NotificationMetadataLink(MetadataLinkType.Imdb, "IMDb", $"https://www.imdb.com/title/{metadata.ImdbId}"));
            }

            if (metadata.YouTubeTrailerId.IsNotNullOrWhiteSpace())
            {
                subject.Links.Add(new NotificationMetadataLink(null, "YouTube", $"https://www.youtube.com/watch?v={metadata.YouTubeTrailerId}"));
            }

            if (metadata.Website.IsNotNullOrWhiteSpace())
            {
                subject.Links.Add(new NotificationMetadataLink(null, "Website", metadata.Website));
            }

            return subject;
        }

        public NotificationFile DescribeFile(Movie movie, MovieFile movieFile)
        {
            if (movieFile == null)
            {
                return null;
            }

            var mediaInfo = movieFile.MediaInfo;

            return new NotificationFile
            {
                Id = movieFile.Id,
                Path = Path.Combine(movie.Path, movieFile.RelativePath),
                RelativePath = movieFile.RelativePath,
                Size = movieFile.Size,
                DateAdded = movieFile.DateAdded,
                SceneName = movieFile.SceneName,
                ReleaseGroup = movieFile.ReleaseGroup,
                QualityName = movieFile.Quality?.Quality?.Name,
                MediaInfo = mediaInfo == null ? null : new NotificationMediaInfo
                {
                    VideoCodec = MediaInfoFormatter.FormatVideoCodec(mediaInfo, null),
                    AudioCodec = MediaInfoFormatter.FormatAudioCodec(mediaInfo, null),
                    AudioChannels = MediaInfoFormatter.FormatAudioChannels(mediaInfo),
                    AudioLanguages = mediaInfo.AudioLanguages ?? new List<string>(),
                    Subtitles = mediaInfo.Subtitles ?? new List<string>()
                }
            };
        }

        public NotificationRelease DescribeRelease(RemoteMovie remoteMovie, QualityModel quality)
        {
            return new NotificationRelease
            {
                Title = remoteMovie.Release.Title,
                Indexer = remoteMovie.Release.Indexer,
                Size = remoteMovie.Release.Size.GetValueOrDefault(),
                ReleaseGroup = remoteMovie.ParsedMovieInfo.ReleaseGroup,
                QualityName = quality.Quality.Name,
                CustomFormats = remoteMovie.CustomFormats.Select(c => c.Name).ToList(),
                CustomFormatScore = remoteMovie.CustomFormatScore
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
