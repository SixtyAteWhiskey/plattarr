using NzbDrone.Core.Modules;
using NzbDrone.Core.Notifications.Trakt.Resource;

namespace NzbDrone.Core.Notifications.Trakt
{
    // Maps a media module's imported file to a Trakt collection entry (resolution, HDR, audio). Trakt keeps
    // movies and shows in separate collections, so each module builds the entry for its own kind. Removing
    // an entry needs only the title, year and ids, which the core reads from the ActivitySubject.
    public interface ITraktCollectionBuilder
    {
        MediaKind Kind { get; }

        TraktCollectMovie BuildCollected(DownloadMessage message);
    }
}
