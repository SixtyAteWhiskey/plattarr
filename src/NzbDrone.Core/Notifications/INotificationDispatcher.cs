namespace NzbDrone.Core.Notifications
{
    // Contract 7 in docs/module-interface.md. A media module builds its neutral messages from its own
    // events and hands them to the core, which picks the providers (enabled for the event, tags, upgrade
    // and file-delete rules), calls them, and records their success or failure.
    public interface INotificationDispatcher
    {
        // True when at least one provider is enabled for the event. A module checks first so it does not
        // build a message (metadata loads, file formatting) that nobody would read.
        bool AnyEnabled(NotificationEvent notificationEvent);

        void Grab(GrabMessage message);
        void Download(DownloadMessage message);
        void Rename(RenameMessage message);
        void MediaAdded(MediaAddedMessage message);
        void MediaFileDelete(MediaFileDeleteMessage message);
        void MediaDelete(MediaDeleteMessage message);
        void ManualInteractionRequired(ManualInteractionRequiredMessage message);
        void ImportComplete(ImportCompleteMessage message);
        void ItemDelete(ItemDeleteMessage message);
        void DownloadFailure(DownloadFailureMessage message);
        void Retag(RetagMessage message);
    }
}
