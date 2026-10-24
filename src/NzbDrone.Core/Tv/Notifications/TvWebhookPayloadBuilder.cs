using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Notifications.Webhook;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Tv.Covers;

namespace NzbDrone.Core.Tv.Notifications
{
    // The TV module's webhook payloads, in Sonarr's upstream shape so existing receivers keep working.
    public class TvWebhookPayloadBuilder : IWebhookPayloadBuilder
    {
        private readonly ITagRepository _tagRepository;
        private readonly IMapCoversToLocal _mediaCoverService;

        public TvWebhookPayloadBuilder(ITagRepository tagRepository, IMapCoversToLocal mediaCoverService)
        {
            _tagRepository = tagRepository;
            _mediaCoverService = mediaCoverService;
        }

        public MediaKind Kind => MediaKind.Tv;

        public WebhookPayload BuildGrab(GrabMessage message)
        {
            var data = (TvNotificationData)message.Data;
            var remoteEpisode = data.RemoteEpisode;

            return new WebhookGrabPayload
            {
                EventType = WebhookEventType.Grab,
                Series = GetSeries(data.Series),
                Episodes = remoteEpisode.Episodes.ConvertAll(x => new WebhookEpisode(x)),
                Release = new WebhookRelease(data.Quality, remoteEpisode),
                DownloadClient = message.DownloadClientName,
                DownloadClientType = message.DownloadClientType,
                DownloadId = message.DownloadId,
                CustomFormatInfo = new WebhookCustomFormatInfo(remoteEpisode.CustomFormats, remoteEpisode.CustomFormatScore)
            };
        }

        public WebhookPayload BuildDownload(DownloadMessage message)
        {
            var data = (TvNotificationData)message.Data;
            var episodeFile = data.EpisodeFile;

            var payload = new WebhookImportPayload
            {
                EventType = WebhookEventType.Download,
                Series = GetSeries(data.Series),
                Episodes = episodeFile.Episodes.Value.ConvertAll(x => new WebhookEpisode(x)),
                EpisodeFile = new WebhookEpisodeFile(episodeFile)
                {
                    SourcePath = message.SourcePath
                },
                Release = new WebhookGrabbedRelease(data.Release, episodeFile.IndexerFlags, episodeFile.ReleaseType),
                IsUpgrade = data.OldEpisodeFiles.Any(),
                DownloadClient = message.DownloadClientInfo?.Name,
                DownloadClientType = message.DownloadClientInfo?.Type,
                DownloadId = message.DownloadId,
                CustomFormatInfo = new WebhookCustomFormatInfo(data.LocalEpisode.CustomFormats, data.LocalEpisode.CustomFormatScore)
            };

            if (data.OldEpisodeFiles.Any())
            {
                payload.DeletedFiles = data.OldEpisodeFiles.ConvertAll(x =>
                    new WebhookEpisodeFile(x.EpisodeFile)
                    {
                        Path = Path.Combine(data.Series.Path, x.EpisodeFile.RelativePath),
                        RecycleBinPath = x.RecycleBinPath
                    });
            }

            return payload;
        }

        public WebhookPayload BuildRename(RenameMessage message)
        {
            var data = (TvNotificationData)message.Data;

            return new WebhookRenamePayload
            {
                EventType = WebhookEventType.Rename,
                Series = GetSeries(data.Series),
                RenamedEpisodeFiles = data.RenamedFiles.ConvertAll(x => new WebhookRenamedEpisodeFile(x))
            };
        }

        public WebhookPayload BuildMediaAdded(MediaAddedMessage message)
        {
            var data = (TvNotificationData)message.Data;

            return new WebhookSeriesAddPayload
            {
                EventType = WebhookEventType.SeriesAdd,
                Series = GetSeries(data.Series)
            };
        }

        public WebhookPayload BuildMediaFileDelete(MediaFileDeleteMessage message)
        {
            var data = (TvNotificationData)message.Data;

            return new WebhookEpisodeDeletePayload
            {
                EventType = WebhookEventType.EpisodeFileDelete,
                Series = GetSeries(data.Series),
                Episodes = data.EpisodeFile.Episodes.Value.ConvertAll(x => new WebhookEpisode(x)),
                EpisodeFile = new WebhookEpisodeFile(data.EpisodeFile),
                DeleteReason = message.Reason
            };
        }

        public WebhookPayload BuildMediaDelete(MediaDeleteMessage message)
        {
            var data = (TvNotificationData)message.Data;

            return new WebhookSeriesDeletePayload
            {
                EventType = WebhookEventType.SeriesDelete,
                Series = GetSeries(data.Series),
                DeletedFiles = message.DeletedFiles
            };
        }

        public WebhookPayload BuildImportComplete(ImportCompleteMessage message)
        {
            var data = (TvNotificationData)message.Data;
            var files = data.EpisodeFiles;

            return new WebhookImportCompletePayload
            {
                EventType = WebhookEventType.Download,
                Series = GetSeries(data.Series),
                Episodes = data.Episodes.ConvertAll(x => new WebhookEpisode(x)),
                EpisodeFiles = files.ConvertAll(x => new WebhookEpisodeFile(x)),
                Release = new WebhookGrabbedRelease(data.Release, files[0].IndexerFlags, files[0].ReleaseType),
                DownloadClient = message.DownloadClientInfo?.Name,
                DownloadClientType = message.DownloadClientInfo?.Type,
                DownloadId = message.DownloadId,
                SourcePath = message.SourcePath,
                DestinationPath = message.DestinationPath
            };
        }

        public WebhookPayload BuildManualInteraction(ManualInteractionRequiredMessage message)
        {
            var data = (TvNotificationData)message.Data;
            var remoteEpisode = data.RemoteEpisode;

            return new WebhookManualInteractionPayload
            {
                EventType = WebhookEventType.ManualInteractionRequired,
                Series = GetSeries(data.Series),
                Episodes = remoteEpisode.Episodes.ConvertAll(x => new WebhookEpisode(x)),
                DownloadInfo = new WebhookDownloadClientItem(data.Quality, message.TrackedDownload.DownloadItem),
                DownloadClient = message.DownloadClientInfo?.Name,
                DownloadClientType = message.DownloadClientInfo?.Type,
                DownloadId = message.DownloadId,
                DownloadStatus = message.TrackedDownload.Status.ToString(),
                DownloadStatusMessages = message.TrackedDownload.StatusMessages.Select(x => new WebhookDownloadStatusMessage(x)).ToList(),
                CustomFormatInfo = new WebhookCustomFormatInfo(remoteEpisode.CustomFormats, remoteEpisode.CustomFormatScore),
                Release = new WebhookGrabbedRelease(data.Release)
            };
        }

        public WebhookPayload BuildItemDelete(ItemDeleteMessage message)
        {
            throw new NotSupportedException("TV has no album delete notification.");
        }

        public WebhookPayload BuildDownloadFailure(DownloadFailureMessage message)
        {
            throw new NotSupportedException("TV has no download failure notification.");
        }

        public WebhookPayload BuildRetag(RetagMessage message)
        {
            throw new NotSupportedException("TV has no retag notification.");
        }

        public WebhookPayload BuildTest()
        {
            return new WebhookGrabPayload
            {
                EventType = WebhookEventType.Test,
                Series = new WebhookSeries
                {
                    Id = 1,
                    Title = "Test Title",
                    Path = "C:\\testpath",
                    TvdbId = 1234,
                    Tags = new List<string> { "test-tag" }
                },
                Episodes = new List<WebhookEpisode>
                {
                    new()
                    {
                        Id = 123,
                        EpisodeNumber = 1,
                        SeasonNumber = 1,
                        Title = "Test title"
                    }
                }
            };
        }

        private WebhookSeries GetSeries(Series series)
        {
            if (series == null)
            {
                return null;
            }

            _mediaCoverService.ConvertToLocalUrls(series.Id, series.Images, series.Added);

            return new WebhookSeries(series, GetTagLabels(series));
        }

        private List<string> GetTagLabels(Series series)
        {
            return _tagRepository.GetTags(series.Tags)
                .Select(t => t.Label)
                .Where(l => l.IsNotNullOrWhiteSpace())
                .OrderBy(l => l)
                .ToList();
        }
    }
}
