using System.Collections.Generic;
using NzbDrone.Common.Messaging;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Download
{
    public class DownloadFailedEvent : IEvent
    {
        public DownloadFailedEvent()
        {
            Data = new Dictionary<string, string>();
            ItemIds = new List<int>();
        }

        // The media type that grabbed the download. Handlers that write a module's own rows
        // ignore an event of another kind. No default: the publisher sets it.
        public MediaKind Kind { get; set; }

        // The movie, series or artist the download was for.
        public int ParentId { get; set; }

        // The library items the download covered: the movie itself, the episodes or the albums.
        public List<int> ItemIds { get; set; }
        public QualityModel Quality { get; set; }
        public string SourceTitle { get; set; }
        public string DownloadClient { get; set; }
        public string DownloadId { get; set; }
        public string Message { get; set; }
        public Dictionary<string, string> Data { get; set; }
        public TrackedDownload TrackedDownload { get; set; }
        public List<Language> Languages { get; set; }
        public bool SkipRedownload { get; set; }
        public ReleaseSourceType ReleaseSource { get; set; }
        // Set by the failed-download search handler after it queues (or joins) a command.
        // A Hygiene action may persist this ID without treating the handoff as a grab.
        public int? ReplacementSearchCommandId { get; set; }
    }
}
