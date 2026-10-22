using System;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Movies.Pending
{
    public class PendingRelease : ModelBase, IMediaKindRecord
    {
        public MediaKind MediaKind { get; set; }
        public int ParentId { get; set; }
        public string Title { get; set; }
        public DateTime Added { get; set; }
        public ParsedMovieInfo ParsedInfo { get; set; }
        public ReleaseInfo Release { get; set; }
        public PendingReleaseReason Reason { get; set; }
        public PendingReleaseAdditionalInfo AdditionalInfo { get; set; }

        // Not persisted
        public RemoteMovie RemoteMovie { get; set; }
    }

    public class PendingReleaseAdditionalInfo
    {
        public MovieMatchType MovieMatchType { get; set; }
        public ReleaseSourceType ReleaseSource { get; set; }
    }
}
