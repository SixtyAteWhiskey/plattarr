using NzbDrone.Core.ThingiProvider;

namespace NzbDrone.Core.Notifications
{
    public interface INotification : IProvider
    {
        string Link { get; }

        void OnGrab(GrabMessage grabMessage);
        void OnDownload(DownloadMessage message);
        void OnRename(RenameMessage message);
        void OnMediaFileDelete(MediaFileDeleteMessage deleteMessage);
        void OnMediaDelete(MediaDeleteMessage deleteMessage);
        void OnMediaAdded(MediaAddedMessage message);
        void OnHealthIssue(HealthCheck.HealthCheck healthCheck);
        void OnHealthRestored(HealthCheck.HealthCheck previousCheck);
        void OnApplicationUpdate(ApplicationUpdateMessage updateMessage);
        void OnManualInteractionRequired(ManualInteractionRequiredMessage message);
        void ProcessQueue();

        // The Supports* names are the wire and database names (v3 API, NotificationDefinition columns) and keep Radarr's spelling.
        bool SupportsOnGrab { get; }
        bool SupportsOnDownload { get; }
        bool SupportsOnUpgrade { get; }
        bool SupportsOnRename { get; }
        bool SupportsOnMovieAdded { get; }
        bool SupportsOnMovieDelete { get; }
        bool SupportsOnMovieFileDelete { get; }
        bool SupportsOnMovieFileDeleteForUpgrade { get; }
        bool SupportsOnHealthIssue { get; }
        bool SupportsOnHealthRestored { get; }
        bool SupportsOnApplicationUpdate { get; }
        bool SupportsOnManualInteractionRequired { get; }
    }
}
