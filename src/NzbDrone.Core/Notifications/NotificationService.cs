using System;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.HealthCheck;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Update.History.Events;

namespace NzbDrone.Core.Notifications
{
    public class NotificationService
        : INotificationDispatcher,
          IHandle<HealthCheckFailedEvent>,
          IHandle<HealthCheckRestoredEvent>,
          IHandle<UpdateInstalledEvent>,
          IHandleAsync<DeleteCompletedEvent>,
          IHandleAsync<DownloadsProcessedEvent>,
          IHandleAsync<RenameCompletedEvent>,
          IHandleAsync<HealthCheckCompleteEvent>
    {
        private readonly INotificationFactory _notificationFactory;
        private readonly INotificationStatusService _notificationStatusService;
        private readonly Logger _logger;

        public NotificationService(INotificationFactory notificationFactory, INotificationStatusService notificationStatusService, Logger logger)
        {
            _notificationFactory = notificationFactory;
            _notificationStatusService = notificationStatusService;
            _logger = logger;
        }

        private bool ShouldHandle(ProviderDefinition definition, ActivitySubject subject)
        {
            if (definition.Tags.Empty())
            {
                _logger.Debug("No tags set for this notification.");
                return true;
            }

            if (subject == null)
            {
                _logger.Debug("{0} has tags but the media item is unknown. Notification will not be sent", definition.Name);
                return false;
            }

            if (definition.Tags.Intersect(subject.Tags).Any())
            {
                _logger.Debug("Notification and media item have one or more intersecting tags.");
                return true;
            }

            // TODO: this message could be more clear
            _logger.Debug("{0} does not have any intersecting tags with {1}. Notification will not be sent", definition.Name, subject.ParentTitle);
            return false;
        }

        private bool ShouldHandleHealthFailure(HealthCheck.HealthCheck healthCheck, bool includeWarnings)
        {
            if (healthCheck.Type == HealthCheckResult.Error)
            {
                return true;
            }

            if (healthCheck.Type == HealthCheckResult.Warning && includeWarnings)
            {
                return true;
            }

            return false;
        }

        public bool AnyEnabled(NotificationEvent notificationEvent)
        {
            return notificationEvent switch
            {
                NotificationEvent.Grab => _notificationFactory.OnGrabEnabled().Any(),
                NotificationEvent.Download => _notificationFactory.OnDownloadEnabled().Any(),
                NotificationEvent.Rename => _notificationFactory.OnRenameEnabled().Any(),
                NotificationEvent.MediaAdded => _notificationFactory.OnMovieAddedEnabled().Any(),
                NotificationEvent.MediaFileDelete => _notificationFactory.OnMovieFileDeleteEnabled().Any(),
                NotificationEvent.MediaDelete => _notificationFactory.OnMovieDeleteEnabled().Any(),
                NotificationEvent.ManualInteractionRequired => _notificationFactory.OnManualInteractionEnabled().Any(),
                NotificationEvent.ImportComplete => _notificationFactory.OnImportCompleteEnabled().Any(),
                NotificationEvent.ItemDelete => _notificationFactory.OnAlbumDeleteEnabled().Any(),
                NotificationEvent.DownloadFailure => _notificationFactory.OnDownloadFailureEnabled().Any(),
                NotificationEvent.Retag => _notificationFactory.OnTrackRetagEnabled().Any(),
                _ => false
            };
        }

        public void Grab(GrabMessage message)
        {
            foreach (var notification in _notificationFactory.OnGrabEnabled())
            {
                try
                {
                    if (!ShouldHandle(notification.Definition, message.Subject))
                    {
                        continue;
                    }

                    notification.OnGrab(message);
                    _notificationStatusService.RecordSuccess(notification.Definition.Id);
                }
                catch (Exception ex)
                {
                    _notificationStatusService.RecordFailure(notification.Definition.Id);
                    _logger.Error(ex, "Unable to send OnGrab notification to {0}", notification.Definition.Name);
                }
            }
        }

        public void Download(DownloadMessage message)
        {
            foreach (var notification in _notificationFactory.OnDownloadEnabled())
            {
                try
                {
                    if (ShouldHandle(notification.Definition, message.Subject))
                    {
                        if (message.OldFiles.Empty() || ((NotificationDefinition)notification.Definition).OnUpgrade)
                        {
                            notification.OnDownload(message);
                            _notificationStatusService.RecordSuccess(notification.Definition.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _notificationStatusService.RecordFailure(notification.Definition.Id);
                    _logger.Warn(ex, "Unable to send OnDownload notification to: " + notification.Definition.Name);
                }
            }
        }

        public void MediaAdded(MediaAddedMessage message)
        {
            foreach (var notification in _notificationFactory.OnMovieAddedEnabled())
            {
                try
                {
                    if (ShouldHandle(notification.Definition, message.Subject))
                    {
                        notification.OnMediaAdded(message);
                        _notificationStatusService.RecordSuccess(notification.Definition.Id);
                    }
                }
                catch (Exception ex)
                {
                    _notificationStatusService.RecordFailure(notification.Definition.Id);
                    _logger.Warn(ex, "Unable to send OnMediaAdded notification to: " + notification.Definition.Name);
                }
            }
        }

        public void Rename(RenameMessage message)
        {
            foreach (var notification in _notificationFactory.OnRenameEnabled())
            {
                try
                {
                    if (ShouldHandle(notification.Definition, message.Subject))
                    {
                        notification.OnRename(message);
                        _notificationStatusService.RecordSuccess(notification.Definition.Id);
                    }
                }
                catch (Exception ex)
                {
                    _notificationStatusService.RecordFailure(notification.Definition.Id);
                    _logger.Warn(ex, "Unable to send OnRename notification to: " + notification.Definition.Name);
                }
            }
        }

        public void ManualInteractionRequired(ManualInteractionRequiredMessage message)
        {
            foreach (var notification in _notificationFactory.OnManualInteractionEnabled())
            {
                try
                {
                    if (!ShouldHandle(notification.Definition, message.Subject))
                    {
                        continue;
                    }

                    notification.OnManualInteractionRequired(message);
                    _notificationStatusService.RecordSuccess(notification.Definition.Id);
                }
                catch (Exception ex)
                {
                    _notificationStatusService.RecordFailure(notification.Definition.Id);
                    _logger.Error(ex, "Unable to send OnManualInteractionRequired notification to {0}", notification.Definition.Name);
                }
            }
        }

        public void ImportComplete(ImportCompleteMessage message)
        {
            foreach (var notification in _notificationFactory.OnImportCompleteEnabled())
            {
                try
                {
                    if (!ShouldHandle(notification.Definition, message.Subject))
                    {
                        continue;
                    }

                    notification.OnImportComplete(message);
                    _notificationStatusService.RecordSuccess(notification.Definition.Id);
                }
                catch (Exception ex)
                {
                    _notificationStatusService.RecordFailure(notification.Definition.Id);
                    _logger.Warn(ex, "Unable to send OnImportComplete notification to: " + notification.Definition.Name);
                }
            }
        }

        public void ItemDelete(ItemDeleteMessage message)
        {
            foreach (var notification in _notificationFactory.OnAlbumDeleteEnabled())
            {
                try
                {
                    if (!ShouldHandle(notification.Definition, message.Subject))
                    {
                        continue;
                    }

                    notification.OnItemDelete(message);
                    _notificationStatusService.RecordSuccess(notification.Definition.Id);
                }
                catch (Exception ex)
                {
                    _notificationStatusService.RecordFailure(notification.Definition.Id);
                    _logger.Warn(ex, "Unable to send OnAlbumDelete notification to: " + notification.Definition.Name);
                }
            }
        }

        public void DownloadFailure(DownloadFailureMessage message)
        {
            foreach (var notification in _notificationFactory.OnDownloadFailureEnabled())
            {
                try
                {
                    if (!ShouldHandle(notification.Definition, message.Subject))
                    {
                        continue;
                    }

                    notification.OnDownloadFailure(message);
                    _notificationStatusService.RecordSuccess(notification.Definition.Id);
                }
                catch (Exception ex)
                {
                    _notificationStatusService.RecordFailure(notification.Definition.Id);
                    _logger.Warn(ex, "Unable to send OnDownloadFailure notification to: " + notification.Definition.Name);
                }
            }
        }

        public void Retag(RetagMessage message)
        {
            foreach (var notification in _notificationFactory.OnTrackRetagEnabled())
            {
                try
                {
                    if (!ShouldHandle(notification.Definition, message.Subject))
                    {
                        continue;
                    }

                    notification.OnRetag(message);
                    _notificationStatusService.RecordSuccess(notification.Definition.Id);
                }
                catch (Exception ex)
                {
                    _notificationStatusService.RecordFailure(notification.Definition.Id);
                    _logger.Warn(ex, "Unable to send OnTrackRetag notification to: " + notification.Definition.Name);
                }
            }
        }

        public void MediaFileDelete(MediaFileDeleteMessage message)
        {
            foreach (var notification in _notificationFactory.OnMovieFileDeleteEnabled())
            {
                try
                {
                    if (message.Reason != DeleteMediaFileReason.Upgrade || ((NotificationDefinition)notification.Definition).OnMovieFileDeleteForUpgrade)
                    {
                        if (ShouldHandle(notification.Definition, message.Subject))
                        {
                            notification.OnMediaFileDelete(message);
                            _notificationStatusService.RecordSuccess(notification.Definition.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _notificationStatusService.RecordFailure(notification.Definition.Id);
                    _logger.Warn(ex, "Unable to send OnMediaFileDelete notification to: " + notification.Definition.Name);
                }
            }
        }

        public void MediaDelete(MediaDeleteMessage message)
        {
            foreach (var notification in _notificationFactory.OnMovieDeleteEnabled())
            {
                try
                {
                    if (ShouldHandle(notification.Definition, message.Subject))
                    {
                        notification.OnMediaDelete(message);
                        _notificationStatusService.RecordSuccess(notification.Definition.Id);
                    }
                }
                catch (Exception ex)
                {
                    _notificationStatusService.RecordFailure(notification.Definition.Id);
                    _logger.Warn(ex, "Unable to send OnMediaDelete notification to: " + notification.Definition.Name);
                }
            }
        }

        public void Handle(UpdateInstalledEvent message)
        {
            var updateMessage = new ApplicationUpdateMessage();
            updateMessage.Message = $"Radarr updated from {message.PreviousVerison.ToString()} to {message.NewVersion.ToString()}";
            updateMessage.PreviousVersion = message.PreviousVerison;
            updateMessage.NewVersion = message.NewVersion;

            foreach (var notification in _notificationFactory.OnApplicationUpdateEnabled())
            {
                try
                {
                    notification.OnApplicationUpdate(updateMessage);
                    _notificationStatusService.RecordSuccess(notification.Definition.Id);
                }
                catch (Exception ex)
                {
                    _notificationStatusService.RecordFailure(notification.Definition.Id);
                    _logger.Warn(ex, "Unable to send OnApplicationUpdate notification to: " + notification.Definition.Name);
                }
            }
        }

        public void Handle(HealthCheckFailedEvent message)
        {
            // Don't send health check notifications during the start up grace period,
            // once that duration expires they they'll be retested and fired off if necessary.
            if (message.IsInStartupGracePeriod)
            {
                return;
            }

            foreach (var notification in _notificationFactory.OnHealthIssueEnabled())
            {
                try
                {
                    if (ShouldHandleHealthFailure(message.HealthCheck, ((NotificationDefinition)notification.Definition).IncludeHealthWarnings))
                    {
                        notification.OnHealthIssue(message.HealthCheck);
                        _notificationStatusService.RecordSuccess(notification.Definition.Id);
                    }
                }
                catch (Exception ex)
                {
                    _notificationStatusService.RecordFailure(notification.Definition.Id);
                    _logger.Warn(ex, "Unable to send OnHealthIssue notification to: " + notification.Definition.Name);
                }
            }
        }

        public void Handle(HealthCheckRestoredEvent message)
        {
            if (message.IsInStartupGracePeriod)
            {
                return;
            }

            foreach (var notification in _notificationFactory.OnHealthRestoredEnabled())
            {
                try
                {
                    if (ShouldHandleHealthFailure(message.PreviousCheck, ((NotificationDefinition)notification.Definition).IncludeHealthWarnings))
                    {
                        notification.OnHealthRestored(message.PreviousCheck);
                        _notificationStatusService.RecordSuccess(notification.Definition.Id);
                    }
                }
                catch (Exception ex)
                {
                    _notificationStatusService.RecordFailure(notification.Definition.Id);
                    _logger.Warn(ex, "Unable to send OnHealthRestored notification to: " + notification.Definition.Name);
                }
            }
        }

        public void HandleAsync(DeleteCompletedEvent message)
        {
            ProcessQueue();
        }

        public void HandleAsync(DownloadsProcessedEvent message)
        {
            ProcessQueue();
        }

        public void HandleAsync(RenameCompletedEvent message)
        {
            ProcessQueue();
        }

        public void HandleAsync(HealthCheckCompleteEvent message)
        {
            ProcessQueue();
        }

        private void ProcessQueue()
        {
            foreach (var notification in _notificationFactory.GetAvailableProviders())
            {
                try
                {
                    notification.ProcessQueue();
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Unable to process notification queue for " + notification.Definition.Name);
                }
            }
        }
    }
}
