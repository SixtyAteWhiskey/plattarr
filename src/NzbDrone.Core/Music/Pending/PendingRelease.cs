using System;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Music.Pending
{
    // The Music module's view of the shared PendingReleases table: ParentId is the artist and
    // ParsedInfo the parsed album info (migration 002 renamed Radarr's columns).
    public class PendingRelease : ModelBase, IMediaKindRecord
    {
        public MediaKind MediaKind { get; set; }
        public int ParentId { get; set; }
        public string Title { get; set; }
        public DateTime Added { get; set; }
        public ParsedAlbumInfo ParsedInfo { get; set; }
        public ReleaseInfo Release { get; set; }
        public PendingReleaseReason Reason { get; set; }
        public PendingReleaseAdditionalInfo AdditionalInfo { get; set; }

        // Not persisted
        public RemoteAlbum RemoteAlbum { get; set; }

        public PendingRelease Clone()
        {
            return MemberwiseClone() as PendingRelease;
        }
    }

    public class PendingReleaseAdditionalInfo
    {
        public ReleaseSourceType ReleaseSource { get; set; }
    }
}
