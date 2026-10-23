using System.Collections.Generic;
using NzbDrone.Common.Messaging;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Download
{
    public class DownloadIgnoredEvent : IEvent
    {
        // The media type that grabbed the download. Handlers that write a module's own rows
        // ignore an event of another kind. No default: the publisher sets it.
        public MediaKind Kind { get; set; }

        // The movie, series or artist the download was for.
        public int ParentId { get; set; }

        // The library items the download covered: the movie itself, the episodes or the tracks.
        public List<int> ItemIds { get; set; } = new();
        public List<Language> Languages { get; set; }
        public QualityModel Quality { get; set; }
        public string SourceTitle { get; set; }
        public DownloadClientItemClientInfo DownloadClientInfo { get; set; }
        public string DownloadId { get; set; }
        public TrackedDownload TrackedDownload { get; set; }
        public string Message { get; set; }
    }
}
