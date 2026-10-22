using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.DecisionEngine
{
    // The outcome of the decision engine for one release. The release is the module's match
    // (RemoteMovie, RemoteEpisode, RemoteAlbum); module code reads it through its own extension
    // (Movies.MovieDecisionExtensions).
    public class DownloadDecision
    {
        public RemoteRelease RemoteRelease { get; private set; }

        public IEnumerable<DownloadRejection> Rejections { get; private set; }

        public bool Approved => !Rejections.Any();

        public bool TemporarilyRejected
        {
            get
            {
                return Rejections.Any() && Rejections.All(r => r.Type == RejectionType.Temporary);
            }
        }

        public bool Rejected
        {
            get
            {
                return Rejections.Any() && Rejections.Any(r => r.Type == RejectionType.Permanent);
            }
        }

        public DownloadDecision(RemoteRelease release, params DownloadRejection[] rejections)
        {
            RemoteRelease = release;
            Rejections = rejections.ToList();
        }

        public override string ToString()
        {
            if (Approved)
            {
                return "[OK] " + RemoteRelease;
            }

            return "[Rejected " + Rejections.Count() + "]" + RemoteRelease;
        }
    }
}
