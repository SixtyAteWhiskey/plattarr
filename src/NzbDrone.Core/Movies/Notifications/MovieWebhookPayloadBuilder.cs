using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.Covers;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Notifications.Webhook;
using NzbDrone.Core.Tags;

namespace NzbDrone.Core.Movies.Notifications
{
    // The Movies module's webhook payloads, in Radarr's upstream shape so existing receivers keep working.
    public class MovieWebhookPayloadBuilder : IWebhookPayloadBuilder
    {
        private readonly ITagRepository _tagRepository;
        private readonly IMapCoversToLocal _mediaCoverService;

        public MovieWebhookPayloadBuilder(ITagRepository tagRepository, IMapCoversToLocal mediaCoverService)
        {
            _tagRepository = tagRepository;
            _mediaCoverService = mediaCoverService;
        }

        public MediaKind Kind => MediaKind.Movies;

        public WebhookPayload BuildGrab(GrabMessage message)
        {
            var data = (MovieNotificationData)message.Data;
            var remoteMovie = data.RemoteMovie;

            return new WebhookGrabPayload
            {
                EventType = WebhookEventType.Grab,
                Movie = GetMovie(data.Movie),
                RemoteMovie = new WebhookRemoteMovie(remoteMovie),
                Release = new WebhookRelease(data.Quality, remoteMovie),
                DownloadClient = message.DownloadClientName,
                DownloadClientType = message.DownloadClientType,
                DownloadId = message.DownloadId,
                CustomFormatInfo = new WebhookCustomFormatInfo(remoteMovie.CustomFormats, remoteMovie.CustomFormatScore)
            };
        }

        public WebhookPayload BuildDownload(DownloadMessage message)
        {
            var data = (MovieNotificationData)message.Data;
            var movieFile = data.MovieFile;

            var payload = new WebhookImportPayload
            {
                EventType = WebhookEventType.Download,
                Movie = GetMovie(data.Movie),
                RemoteMovie = new WebhookRemoteMovie(data.Movie),
                MovieFile = new WebhookMovieFile(movieFile)
                {
                    SourcePath = message.SourcePath
                },
                Release = new WebhookGrabbedRelease(data.Release, movieFile.IndexerFlags),
                IsUpgrade = data.OldMovieFiles.Any(),
                DownloadClient = message.DownloadClientInfo?.Name,
                DownloadClientType = message.DownloadClientInfo?.Type,
                DownloadId = message.DownloadId,
                CustomFormatInfo = new WebhookCustomFormatInfo(data.LocalMovie.CustomFormats, data.LocalMovie.CustomFormatScore)
            };

            if (data.OldMovieFiles.Any())
            {
                payload.DeletedFiles = data.OldMovieFiles.ConvertAll(x =>
                    new WebhookMovieFile(x.MovieFile)
                    {
                        Path = Path.Combine(data.Movie.Path, x.MovieFile.RelativePath),
                        RecycleBinPath = x.RecycleBinPath
                    });
            }

            return payload;
        }

        public WebhookPayload BuildRename(RenameMessage message)
        {
            var data = (MovieNotificationData)message.Data;

            return new WebhookRenamePayload
            {
                EventType = WebhookEventType.Rename,
                Movie = GetMovie(data.Movie),
                RenamedMovieFiles = data.RenamedFiles.ConvertAll(x => new WebhookRenamedMovieFile(x))
            };
        }

        public WebhookPayload BuildMediaAdded(MediaAddedMessage message)
        {
            var movie = ((MovieNotificationData)message.Data).Movie;

            return new WebhookAddedPayload
            {
                EventType = WebhookEventType.MovieAdded,
                Movie = GetMovie(movie),
                AddMethod = movie.AddOptions.AddMethod
            };
        }

        public WebhookPayload BuildMediaFileDelete(MediaFileDeleteMessage message)
        {
            var data = (MovieNotificationData)message.Data;

            return new WebhookMovieFileDeletePayload
            {
                EventType = WebhookEventType.MovieFileDelete,
                Movie = GetMovie(data.Movie),
                MovieFile = new WebhookMovieFile(data.MovieFile),
                DeleteReason = message.Reason
            };
        }

        public WebhookPayload BuildMediaDelete(MediaDeleteMessage message)
        {
            var movie = ((MovieNotificationData)message.Data).Movie;

            var payload = new WebhookMovieDeletePayload
            {
                EventType = WebhookEventType.MovieDelete,
                Movie = GetMovie(movie),
                DeletedFiles = message.DeletedFiles
            };

            if (message.DeletedFiles && movie.MovieFile != null)
            {
                payload.MovieFolderSize = movie.MovieFile.Size;
            }

            return payload;
        }

        public WebhookPayload BuildImportComplete(ImportCompleteMessage message)
        {
            // A movie download imports one file, which OnDownload already reports.
            throw new NotSupportedException("Movies send no import-complete event.");
        }

        public WebhookPayload BuildManualInteraction(ManualInteractionRequiredMessage message)
        {
            var data = (MovieNotificationData)message.Data;

            return new WebhookManualInteractionPayload
            {
                EventType = WebhookEventType.ManualInteractionRequired,
                Movie = GetMovie(data.Movie),
                DownloadInfo = new WebhookDownloadClientItem(data.Quality, message.TrackedDownload.DownloadItem),
                DownloadClient = message.DownloadClientInfo?.Name,
                DownloadClientType = message.DownloadClientInfo?.Type,
                DownloadId = message.DownloadId,
                DownloadStatus = message.TrackedDownload.Status.ToString(),
                DownloadStatusMessages = message.TrackedDownload.StatusMessages.Select(x => new WebhookDownloadStatusMessage(x)).ToList(),
                CustomFormatInfo = new WebhookCustomFormatInfo(data.RemoteMovie.CustomFormats, data.RemoteMovie.CustomFormatScore),
                Release = new WebhookGrabbedRelease(data.Release)
            };
        }

        public WebhookPayload BuildTest()
        {
            return new WebhookGrabPayload
            {
                EventType = WebhookEventType.Test,
                Movie = new WebhookMovie
                {
                    Id = 1,
                    Title = "Test Title",
                    Year = 1970,
                    FolderPath = "C:\\testpath",
                    ReleaseDate = "1970-01-01",
                    Tags = new List<string> { "test-tag" }
                },
                RemoteMovie = new WebhookRemoteMovie
                {
                    TmdbId = 1234,
                    ImdbId = "5678",
                    Title = "Test title",
                    Year = 1970
                },
                Release = new WebhookRelease
                {
                    Indexer = "Test Indexer",
                    Quality = "Test Quality",
                    QualityVersion = 1,
                    ReleaseGroup = "Test Group",
                    ReleaseTitle = "Test Title",
                    Size = 9999999
                }
            };
        }

        private WebhookMovie GetMovie(Movie movie)
        {
            if (movie == null)
            {
                return null;
            }

            _mediaCoverService.ConvertToLocalUrls(movie.Id, movie.MovieMetadata.Value.Images, movie.Added);

            return new WebhookMovie(movie, GetTagLabels(movie));
        }

        private List<string> GetTagLabels(Movie movie)
        {
            return _tagRepository.GetTags(movie.Tags)
                .Select(t => t.Label)
                .Where(l => l.IsNotNullOrWhiteSpace())
                .OrderBy(l => l)
                .ToList();
        }
    }
}
