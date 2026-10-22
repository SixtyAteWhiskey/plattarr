using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Tv.History
{
    // The TV module's view of the shared History table. Sonarr keeps one row per episode:
    // ParentId is the series and ItemId the episode (migration 004 adds ItemId).
    public class EpisodeHistory : ModelBase, IMediaKindRecord
    {
        public const string DOWNLOAD_CLIENT = "downloadClient";
        public const string SERIES_MATCH_TYPE = "seriesMatchType";
        public const string RELEASE_SOURCE = "releaseSource";
        public const string RELEASE_GROUP = "releaseGroup";
        public const string SIZE = "size";
        public const string INDEXER = "indexer";

        public EpisodeHistory()
        {
            Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public MediaKind MediaKind { get; set; }
        public int ParentId { get; set; }
        public int ItemId { get; set; }
        public string SourceTitle { get; set; }
        public QualityModel Quality { get; set; }
        public DateTime Date { get; set; }
        public Episode Episode { get; set; }
        public Series Series { get; set; }
        public EpisodeHistoryEventType EventType { get; set; }
        public Dictionary<string, string> Data { get; set; }
        public List<Language> Languages { get; set; }
        public string DownloadId { get; set; }
    }

    // Sonarr's values, so the importer and the Sonarr v3 API map one to one. They are stored
    // beside the movie module's own values, told apart by MediaKind.
    public enum EpisodeHistoryEventType
    {
        Unknown = 0,
        Grabbed = 1,
        SeriesFolderImported = 2,
        DownloadFolderImported = 3,
        DownloadFailed = 4,
        EpisodeFileDeleted = 5,
        EpisodeFileRenamed = 6,
        DownloadIgnored = 7
    }
}
