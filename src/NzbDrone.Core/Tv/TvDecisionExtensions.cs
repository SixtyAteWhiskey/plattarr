using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Tv
{
    // The core carries the module's match as a RemoteRelease; TV code reads it as a RemoteEpisode.
    public static class TvDecisionExtensions
    {
        public static RemoteEpisode GetRemoteEpisode(this DownloadDecision decision)
        {
            return decision.RemoteRelease as RemoteEpisode;
        }

        public static RemoteEpisode GetRemoteEpisode(this ReleaseGrabbedEvent grabbed)
        {
            return grabbed.Release as RemoteEpisode;
        }
    }
}
