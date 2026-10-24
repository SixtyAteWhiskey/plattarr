using System.Collections.Generic;
using System.Linq;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Notifications.Emby
{
    public class MediaBrowser : NotificationBase<MediaBrowserSettings>
    {
        private readonly IMediaBrowserService _mediaBrowserService;
        private readonly MediaServerUpdateQueue<MediaBrowser, string> _updateQueue;
        private readonly Logger _logger;

        private static string Created = "Created";
        private static string Deleted = "Deleted";
        private static string Modified = "Modified";

        public MediaBrowser(IMediaBrowserService mediaBrowserService, ICacheManager cacheManager, Logger logger)
        {
            _mediaBrowserService = mediaBrowserService;
            _updateQueue = new MediaServerUpdateQueue<MediaBrowser, string>(cacheManager);
            _logger = logger;
        }

        public override string Link => "https://emby.media/";
        public override string Name => "Emby / Jellyfin";

        public override void OnGrab(GrabMessage grabMessage)
        {
            if (Settings.Notify)
            {
                _mediaBrowserService.Notify(Settings, Branded(GrabbedTitle(grabMessage.Subject)), grabMessage.Message);
            }
        }

        public override void OnDownload(DownloadMessage message)
        {
            if (Settings.Notify)
            {
                _mediaBrowserService.Notify(Settings, Branded(DownloadedTitle(message.Subject)), message.Message);
            }

            UpdateIfEnabled(message.Subject, Created);
        }

        public override void OnRename(RenameMessage message)
        {
            UpdateIfEnabled(message.Subject, Modified);
        }

        public override void OnHealthIssue(HealthCheck.HealthCheck message)
        {
            if (Settings.Notify)
            {
                _mediaBrowserService.Notify(Settings, HEALTH_ISSUE_TITLE_BRANDED, message.Message);
            }
        }

        public override void OnHealthRestored(HealthCheck.HealthCheck previousMessage)
        {
            if (Settings.Notify)
            {
                _mediaBrowserService.Notify(Settings, HEALTH_RESTORED_TITLE_BRANDED, $"The following issue is now resolved: {previousMessage.Message}");
            }
        }

        public override void OnApplicationUpdate(ApplicationUpdateMessage updateMessage)
        {
            if (Settings.Notify)
            {
                _mediaBrowserService.Notify(Settings, APPLICATION_UPDATE_TITLE_BRANDED, updateMessage.Message);
            }
        }

        public override void OnMediaDelete(MediaDeleteMessage deleteMessage)
        {
            if (deleteMessage.DeletedFiles)
            {
                if (Settings.Notify)
                {
                    _mediaBrowserService.Notify(Settings, Branded(DeletedTitle(deleteMessage.Subject)), deleteMessage.Message);
                }

                UpdateIfEnabled(deleteMessage.Subject, Deleted);
            }
        }

        public override void OnMediaFileDelete(MediaFileDeleteMessage deleteMessage)
        {
            if (Settings.Notify)
            {
                _mediaBrowserService.Notify(Settings, Branded(FileDeletedTitle(deleteMessage.Subject)), deleteMessage.Message);
            }

            UpdateIfEnabled(deleteMessage.Subject, Deleted);
        }

        // As Lidarr's provider: an album removed from an artist is announced and the library is refreshed,
        // a retag is announced only. A failed download has no handler here (Lidarr's has none either).
        // The artist folder still exists after an album goes, so the refresh is "Modified", not "Deleted".
        public override void OnItemDelete(ItemDeleteMessage deleteMessage)
        {
            var subject = deleteMessage.Subject;

            if (Settings.Notify)
            {
                var title = subject == null ? "Album Deleted" : ItemDeletedTitle(subject);

                _mediaBrowserService.Notify(Settings, Branded(title), deleteMessage.Message);
            }

            if (subject != null)
            {
                UpdateIfEnabled(subject, Modified);
            }
        }

        public override void OnRetag(RetagMessage message)
        {
            if (Settings.Notify)
            {
                _mediaBrowserService.Notify(Settings, Branded(TRACK_RETAGGED_TITLE), message.Message);
            }
        }

        public override void ProcessQueue()
        {
            _updateQueue.ProcessQueue(Settings.Host, (items) =>
            {
                if (Settings.UpdateLibrary)
                {
                    _logger.Debug("Performing library update for {0} items", items.Count);

                    items.ForEach(item =>
                    {
                        // If there is only one update type for the movie use that, otherwise send null and let Emby decide
                        var updateType = item.Info.Count == 1 ? item.Info.First() : null;

                        _mediaBrowserService.Update(Settings, item.Subject, updateType);
                    });
                }
            });
        }

        private void UpdateIfEnabled(ActivitySubject subject, string updateType)
        {
            if (Settings.UpdateLibrary)
            {
                _logger.Debug("Scheduling library update for {0} {1} {2}", subject.KindLabel, subject.ParentId, subject.ParentTitle);
                _updateQueue.Add(Settings.Host, subject, updateType);
            }
        }

        public override ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();

            failures.AddIfNotNull(_mediaBrowserService.Test(Settings));

            return new ValidationResult(failures);
        }
    }
}
