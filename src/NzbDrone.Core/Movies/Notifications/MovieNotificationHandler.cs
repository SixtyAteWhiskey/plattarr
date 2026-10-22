using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.Events;
using NzbDrone.Core.Movies.MediaFiles.Events;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Movies.Notifications
{
    // Contract 7. Movie events become neutral notification messages for the core dispatcher.
    public class MovieNotificationHandler
        : IHandle<ReleaseGrabbedEvent>,
          IHandle<MovieFileImportedEvent>,
          IHandle<MovieAddedEvent>,
          IHandle<MoviesImportedEvent>,
          IHandle<MovieRenamedEvent>,
          IHandle<MovieFileDeletedEvent>,
          IHandle<MoviesDeletedEvent>,
          IHandle<ManualInteractionRequiredEvent>
    {
        private readonly INotificationDispatcher _dispatcher;
        private readonly MovieActivityRenderer _renderer;

        public MovieNotificationHandler(INotificationDispatcher dispatcher, MovieActivityRenderer renderer)
        {
            _dispatcher = dispatcher;
            _renderer = renderer;
        }

        // "{title} ({year}) [{quality}[ Proper]] https://www.imdb.com/title/{imdb}/"
        private static string GetMessage(Movie movie, QualityModel quality)
        {
            var qualityString = quality.Quality.ToString();
            var imdbUrl = "https://www.imdb.com/title/" + movie.MovieMetadata.Value.ImdbId + "/";

            if (quality.Revision.Version > 1)
            {
                qualityString += " Proper";
            }

            return string.Format("{0} ({1}) [{2}] {3}",
                                    movie.Title,
                                    movie.Year,
                                    qualityString,
                                    imdbUrl);
        }

        public void Handle(ReleaseGrabbedEvent message)
        {
            if (message.Release.Kind != MediaKind.Movies)
            {
                return;
            }

            var remoteMovie = message.GetRemoteMovie();

            if (!_dispatcher.AnyEnabled(NotificationEvent.Grab))
            {
                return;
            }

            var movie = remoteMovie.Movie;
            var quality = remoteMovie.ParsedMovieInfo.Quality;

            _dispatcher.Grab(new GrabMessage
            {
                Message = GetMessage(movie, quality),
                Subject = _renderer.Describe(movie),
                Data = new MovieNotificationData { Movie = movie, RemoteMovie = remoteMovie, Quality = quality },
                QualityName = quality.Quality.Name,
                Release = _renderer.DescribeRelease(remoteMovie, quality),
                DownloadClientType = message.DownloadClient,
                DownloadClientName = message.DownloadClientName,
                DownloadId = message.DownloadId
            });
        }

        public void Handle(MovieFileImportedEvent message)
        {
            if (!message.NewDownload || !_dispatcher.AnyEnabled(NotificationEvent.Download))
            {
                return;
            }

            var movie = message.MovieInfo.Movie;
            var oldFiles = message.OldFiles ?? new List<MediaFiles.DeletedMovieFile>();

            _dispatcher.Download(new DownloadMessage
            {
                Message = GetMessage(movie, message.MovieInfo.Quality),
                Subject = _renderer.Describe(movie),
                Data = new MovieNotificationData
                {
                    Movie = movie,
                    LocalMovie = message.MovieInfo,
                    MovieFile = message.ImportedMovie,
                    OldMovieFiles = oldFiles,
                    Release = message.MovieInfo.Release
                },
                File = _renderer.DescribeFile(movie, message.ImportedMovie),
                OldFiles = oldFiles.Select(f => _renderer.DescribeFile(movie, f.MovieFile)).ToList(),
                SourcePath = message.MovieInfo.Path,
                DownloadClientInfo = message.DownloadClientInfo,
                DownloadId = message.DownloadId,
                CustomFormats = message.MovieInfo.CustomFormats.Select(c => c.Name).ToList(),
                CustomFormatScore = message.MovieInfo.CustomFormatScore
            });
        }

        public void Handle(MovieAddedEvent message)
        {
            Added(message.Movie);
        }

        public void Handle(MoviesImportedEvent message)
        {
            foreach (var movie in message.Movies)
            {
                Added(movie);
            }
        }

        private void Added(Movie movie)
        {
            if (!_dispatcher.AnyEnabled(NotificationEvent.MediaAdded))
            {
                return;
            }

            _dispatcher.MediaAdded(new MediaAddedMessage
            {
                Subject = _renderer.Describe(movie),
                Data = new MovieNotificationData { Movie = movie }
            });
        }

        public void Handle(MovieRenamedEvent message)
        {
            if (!_dispatcher.AnyEnabled(NotificationEvent.Rename))
            {
                return;
            }

            _dispatcher.Rename(new RenameMessage
            {
                Subject = _renderer.Describe(message.Movie),
                Data = new MovieNotificationData { Movie = message.Movie, RenamedFiles = message.RenamedFiles },
                Files = message.RenamedFiles.ConvertAll(f => new NotificationRenamedFile
                {
                    File = _renderer.DescribeFile(message.Movie, f.MovieFile),
                    PreviousPath = f.PreviousPath,
                    PreviousRelativePath = f.PreviousRelativePath
                })
            });
        }

        public void Handle(MovieFileDeletedEvent message)
        {
            if (!_dispatcher.AnyEnabled(NotificationEvent.MediaFileDelete))
            {
                return;
            }

            var movie = message.MovieFile.Movie;

            _dispatcher.MediaFileDelete(new MediaFileDeleteMessage
            {
                Message = GetMessage(movie, message.MovieFile.Quality),
                Subject = _renderer.Describe(movie),
                Data = new MovieNotificationData { Movie = movie, MovieFile = message.MovieFile },
                File = _renderer.DescribeFile(movie, message.MovieFile),
                Reason = message.Reason
            });
        }

        public void Handle(MoviesDeletedEvent message)
        {
            if (!_dispatcher.AnyEnabled(NotificationEvent.MediaDelete))
            {
                return;
            }

            foreach (var movie in message.Movies)
            {
                _dispatcher.MediaDelete(new MediaDeleteMessage(
                    _renderer.Describe(movie),
                    new MovieNotificationData { Movie = movie },
                    message.DeleteFiles));
            }
        }

        public void Handle(ManualInteractionRequiredEvent message)
        {
            if (message.Data is not MovieNotificationData published || !_dispatcher.AnyEnabled(NotificationEvent.ManualInteractionRequired))
            {
                return;
            }

            var trackedDownload = message.TrackedDownload;
            var remoteMovie = trackedDownload.RemoteMovie;
            var movie = remoteMovie?.Movie;
            var text = "";

            if (movie != null)
            {
                text = GetMessage(movie, remoteMovie.ParsedMovieInfo.Quality);
            }

            if (text.IsNullOrWhiteSpace() && trackedDownload.DownloadItem != null)
            {
                text = trackedDownload.DownloadItem.Title;
            }

            if (text.IsNullOrWhiteSpace())
            {
                return;
            }

            var data = new MovieNotificationData
            {
                Movie = movie,
                RemoteMovie = remoteMovie,
                Quality = remoteMovie?.ParsedMovieInfo?.Quality,
                Release = published.Release
            };

            _dispatcher.ManualInteractionRequired(new ManualInteractionRequiredMessage
            {
                Message = text,
                Subject = _renderer.Describe(movie),
                Data = data,
                TrackedDownload = trackedDownload,
                QualityName = data.Quality?.Quality?.Name,
                ReleaseGroup = remoteMovie?.ParsedMovieInfo?.ReleaseGroup,
                DownloadClientInfo = trackedDownload.DownloadItem?.DownloadClientInfo,
                DownloadId = trackedDownload.DownloadItem?.DownloadId,
                Release = _renderer.DescribeRelease(data.Release)
            });
        }
    }
}
