using System;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Tv.Pending
{
    // The TV module's view of the shared PendingReleases table: ParentId is the series and
    // ParsedInfo the parsed episode info (migration 002 renamed Radarr's columns).
    public class PendingRelease : ModelBase, IMediaKindRecord
    {
        public MediaKind MediaKind { get; set; }
        public int ParentId { get; set; }
        public string Title { get; set; }
        public DateTime Added { get; set; }
        public ParsedEpisodeInfo ParsedInfo { get; set; }
        public ReleaseInfo Release { get; set; }
        public PendingReleaseReason Reason { get; set; }
        public PendingReleaseAdditionalInfo AdditionalInfo { get; set; }

        // Not persisted
        public RemoteEpisode RemoteEpisode { get; set; }

        public PendingRelease Clone()
        {
            return MemberwiseClone() as PendingRelease;
        }
    }

    public class PendingReleaseAdditionalInfo
    {
        public SeriesMatchType SeriesMatchType { get; set; }
        public ReleaseSourceType ReleaseSource { get; set; }
    }
}
