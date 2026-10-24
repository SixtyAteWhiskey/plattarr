using System.Collections.Specialized;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Notifications.CustomScript
{
    // A media module's script environment. It returns the variables that describe the media (Radarr_Movie_*,
    // Radarr_MovieFile_*, Radarr_Release_*); the core adds Radarr_EventType, Radarr_InstanceName and
    // Radarr_ApplicationUrl. A module keeps its upstream variable names so existing scripts keep working.
    public interface ICustomScriptEnvironmentBuilder
    {
        MediaKind Kind { get; }

        // "Radarr", "Sonarr": the variable prefix the module's upstream scripts expect. The core uses it for
        // the event type, instance name and application URL variables it adds ({Prefix}_EventType).
        string VariablePrefix { get; }

        // The EventType value a script sees for an event: "MovieAdded" for a movie, "SeriesAdd" for a series.
        string EventType(NotificationEvent notificationEvent);

        StringDictionary BuildGrab(GrabMessage message);
        StringDictionary BuildDownload(DownloadMessage message);
        StringDictionary BuildRename(RenameMessage message);
        StringDictionary BuildMediaAdded(MediaAddedMessage message);
        StringDictionary BuildMediaFileDelete(MediaFileDeleteMessage message);
        StringDictionary BuildMediaDelete(MediaDeleteMessage message);
        StringDictionary BuildManualInteraction(ManualInteractionRequiredMessage message);

        // A module that never sends the event throws NotSupportedException. Lidarr ran no script for a failed download.
        StringDictionary BuildItemDelete(ItemDeleteMessage message);
        StringDictionary BuildRetag(RetagMessage message);
    }
}
