namespace NzbDrone.Core.Notifications
{
    public enum NotificationEvent
    {
        Grab,
        Download,
        Rename,
        MediaAdded,
        MediaFileDelete,
        MediaDelete,
        ManualInteractionRequired
    }
}
