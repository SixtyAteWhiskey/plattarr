using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Movies
{
    // The core carries the module's match as a RemoteRelease; movie code reads it as a RemoteMovie.
    public static class MovieDecisionExtensions
    {
        public static RemoteMovie GetRemoteMovie(this DownloadDecision decision)
        {
            return decision.RemoteRelease as RemoteMovie;
        }

        public static RemoteMovie GetRemoteMovie(this ReleaseGrabbedEvent grabbed)
        {
            return grabbed.Release as RemoteMovie;
        }
    }
}
