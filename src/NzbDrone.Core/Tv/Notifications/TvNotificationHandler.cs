using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv.Events;
using NzbDrone.Core.Tv.MediaFiles;
using NzbDrone.Core.Tv.MediaFiles.Events;

namespace NzbDrone.Core.Tv.Notifications
{
    // Contract 7. TV events become neutral notification messages for the core dispatcher. The message
    // text is Sonarr's own ("Series - 1x01 - Title [WEBDL-1080p]").
    public class TvNotificationHandler
        : IHandle<ReleaseGrabbedEvent>,
          IHandle<EpisodeImportedEvent>,
          IHandle<SeriesAddCompletedEvent>,
          IHandle<SeriesRenamedEvent>,
          IHandle<EpisodeFileDeletedEvent>,
          IHandle<SeriesDeletedEvent>,
          IHandle<ManualInteractionRequiredEvent>
    {
        private readonly INotificationDispatcher _dispatcher;
        private readonly TvActivityRenderer _renderer;

        public TvNotificationHandler(INotificationDispatcher dispatcher, TvActivityRenderer renderer)
        {
            _dispatcher = dispatcher;
            _renderer = renderer;
        }

        public static string GetMessage(Series series, IReadOnlyList<Episode> episodes, QualityModel quality)
        {
            var qualityString = GetQualityString(series, quality);

            if (episodes == null || episodes.Count == 0)
            {
                return $"{series.Title} - [{qualityString}]";
            }

            if (series.SeriesType == SeriesTypes.Daily)
            {
                var episode = episodes[0];

                return $"{series.Title} - {episode.AirDate} - {episode.Title} [{qualityString}]";
            }

            var episodeNumbers = string.Concat(episodes.Select(e => $"x{e.EpisodeNumber:00}"));
            var episodeTitles = string.Join(" + ", episodes.Select(e => e.Title));

            return $"{series.Title} - {episodes[0].SeasonNumber}{episodeNumbers} - {episodeTitles} [{qualityString}]";
        }

        private static string GetQualityString(Series series, QualityModel quality)
        {
            var qualityString = quality.Quality.ToString();

            if (quality.Revision.Version > 1)
            {
                qualityString += series.SeriesType == SeriesTypes.Anime
                    ? " v" + quality.Revision.Version
                    : " Proper";
            }

            return qualityString;
        }

        public void Handle(ReleaseGrabbedEvent message)
        {
            if (message.Release.Kind != MediaKind.Tv)
            {
                return;
            }

            if (!_dispatcher.AnyEnabled(NotificationEvent.Grab))
            {
                return;
            }

            var remoteEpisode = message.GetRemoteEpisode();
            var series = remoteEpisode.Series;
            var quality = remoteEpisode.ParsedEpisodeInfo.Quality;

            _dispatcher.Grab(new GrabMessage
            {
                Message = GetMessage(series, remoteEpisode.Episodes, quality),
                Subject = _renderer.Describe(series, remoteEpisode.Episodes),
                Data = new TvNotificationData { Series = series, RemoteEpisode = remoteEpisode, Quality = quality },
                QualityName = quality.Quality.Name,
                Release = _renderer.DescribeRelease(remoteEpisode, quality),
                DownloadClientType = message.DownloadClient,
                DownloadClientName = message.DownloadClientName,
                DownloadId = message.DownloadId
            });
        }

        public void Handle(EpisodeImportedEvent message)
        {
            if (!message.NewDownload || !_dispatcher.AnyEnabled(NotificationEvent.Download))
            {
                return;
            }

            var series = message.EpisodeInfo.Series;
            var oldFiles = message.OldFiles ?? new List<DeletedEpisodeFile>();

            _dispatcher.Download(new DownloadMessage
            {
                Message = GetMessage(series, message.EpisodeInfo.Episodes, message.EpisodeInfo.Quality),
                Subject = _renderer.Describe(series, message.EpisodeInfo.Episodes),
                Data = new TvNotificationData
                {
                    Series = series,
                    LocalEpisode = message.EpisodeInfo,
                    EpisodeFile = message.ImportedEpisode,
                    OldEpisodeFiles = oldFiles,
                    Release = message.EpisodeInfo.Release
                },
                File = _renderer.DescribeFile(series, message.ImportedEpisode),
                OldFiles = oldFiles.Select(f => _renderer.DescribeFile(series, f.EpisodeFile)).ToList(),
                SourcePath = message.EpisodeInfo.Path,
                DownloadClientInfo = message.DownloadClientInfo,
                DownloadId = message.DownloadId,
                CustomFormats = message.EpisodeInfo.CustomFormats.Select(c => c.Name).ToList(),
                CustomFormatScore = message.EpisodeInfo.CustomFormatScore
            });
        }

        public void Handle(SeriesAddCompletedEvent message)
        {
            if (!_dispatcher.AnyEnabled(NotificationEvent.MediaAdded))
            {
                return;
            }

            _dispatcher.MediaAdded(new MediaAddedMessage
            {
                Subject = _renderer.Describe(message.Series),
                Data = new TvNotificationData { Series = message.Series }
            });
        }

        public void Handle(SeriesRenamedEvent message)
        {
            if (!_dispatcher.AnyEnabled(NotificationEvent.Rename))
            {
                return;
            }

            _dispatcher.Rename(new RenameMessage
            {
                Subject = _renderer.Describe(message.Series),
                Data = new TvNotificationData { Series = message.Series, RenamedFiles = message.RenamedFiles },
                Files = message.RenamedFiles.ConvertAll(f => new NotificationRenamedFile
                {
                    File = _renderer.DescribeFile(message.Series, f.EpisodeFile),
                    PreviousPath = f.PreviousPath,
                    PreviousRelativePath = f.PreviousRelativePath
                })
            });
        }

        public void Handle(EpisodeFileDeletedEvent message)
        {
            if (!_dispatcher.AnyEnabled(NotificationEvent.MediaFileDelete))
            {
                return;
            }

            // The episode rows are gone when the series metadata was removed first; Sonarr sends nothing.
            var episodes = message.EpisodeFile.Episodes?.Value;

            if (episodes == null || episodes.Count == 0)
            {
                return;
            }

            var series = message.EpisodeFile.Series.Value;

            _dispatcher.MediaFileDelete(new MediaFileDeleteMessage
            {
                Message = GetMessage(series, episodes, message.EpisodeFile.Quality),
                Subject = _renderer.Describe(series, episodes),
                Data = new TvNotificationData { Series = series, EpisodeFile = message.EpisodeFile },
                File = _renderer.DescribeFile(series, message.EpisodeFile),
                Reason = message.Reason
            });
        }

        public void Handle(SeriesDeletedEvent message)
        {
            if (!_dispatcher.AnyEnabled(NotificationEvent.MediaDelete))
            {
                return;
            }

            foreach (var series in message.Series)
            {
                _dispatcher.MediaDelete(new MediaDeleteMessage(
                    _renderer.Describe(series),
                    new TvNotificationData { Series = series },
                    message.DeleteFiles));
            }
        }

        public void Handle(ManualInteractionRequiredEvent message)
        {
            if (message.Data is not TvNotificationData published || !_dispatcher.AnyEnabled(NotificationEvent.ManualInteractionRequired))
            {
                return;
            }

            var trackedDownload = message.TrackedDownload;
            var remoteEpisode = trackedDownload.GetRemoteEpisode();
            var series = remoteEpisode?.Series;
            var text = "";

            if (series != null)
            {
                text = GetMessage(series, remoteEpisode.Episodes, remoteEpisode.ParsedEpisodeInfo.Quality);
            }

            if (text.IsNullOrWhiteSpace() && trackedDownload.DownloadItem != null)
            {
                text = trackedDownload.DownloadItem.Title;
            }

            if (text.IsNullOrWhiteSpace())
            {
                return;
            }

            var data = new TvNotificationData
            {
                Series = series,
                RemoteEpisode = remoteEpisode,
                Quality = remoteEpisode?.ParsedEpisodeInfo?.Quality,
                Release = published.Release
            };

            _dispatcher.ManualInteractionRequired(new ManualInteractionRequiredMessage
            {
                Message = text,
                Subject = _renderer.Describe(series, remoteEpisode?.Episodes),
                Data = data,
                TrackedDownload = trackedDownload,
                QualityName = data.Quality?.Quality?.Name,
                ReleaseGroup = remoteEpisode?.ParsedEpisodeInfo?.ReleaseGroup,
                DownloadClientInfo = trackedDownload.DownloadItem?.DownloadClientInfo,
                DownloadId = trackedDownload.DownloadItem?.DownloadId,
                Release = _renderer.DescribeRelease(data.Release)
            });
        }
    }
}
