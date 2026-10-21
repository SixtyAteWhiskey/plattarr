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

        StringDictionary BuildGrab(GrabMessage message);
        StringDictionary BuildDownload(DownloadMessage message);
        StringDictionary BuildRename(RenameMessage message);
        StringDictionary BuildMediaAdded(MediaAddedMessage message);
        StringDictionary BuildMediaFileDelete(MediaFileDeleteMessage message);
        StringDictionary BuildMediaDelete(MediaDeleteMessage message);
        StringDictionary BuildManualInteraction(ManualInteractionRequiredMessage message);
    }
}
