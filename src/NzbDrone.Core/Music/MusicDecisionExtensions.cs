using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Music.Parser.Model;

namespace NzbDrone.Core.Music
{
    // The core carries the module's match as a RemoteRelease; Music code reads it as a RemoteAlbum.
    public static class MusicDecisionExtensions
    {
        public static RemoteAlbum GetRemoteAlbum(this DownloadDecision decision)
        {
            return decision.RemoteRelease as RemoteAlbum;
        }

        public static RemoteAlbum GetRemoteAlbum(this ReleaseGrabbedEvent grabbed)
        {
            return grabbed.Release as RemoteAlbum;
        }
    }
}
