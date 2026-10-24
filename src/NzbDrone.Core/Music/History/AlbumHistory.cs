using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Music.History
{
    // The Music module's view of the shared History table. Lidarr keeps one row per album grab
    // and one per track file event: ParentId is the artist and ItemId the album. Lidarr's TrackId
    // (the track a file event is about) is the "trackId" entry of Data: the shared table has no
    // column for it and nothing filters on it (call made in step 6).
    public class AlbumHistory : ModelBase, IMediaKindRecord
    {
        public const string DOWNLOAD_CLIENT = "downloadClient";
        public const string RELEASE_SOURCE = "releaseSource";
        public const string RELEASE_GROUP = "releaseGroup";
        public const string SIZE = "size";
        public const string INDEXER = "indexer";
        public const string TRACK_ID = "trackId";

        public AlbumHistory()
        {
            Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Languages = new List<Language>();
        }

        public MediaKind MediaKind { get; set; }
        public int ParentId { get; set; }
        public int ItemId { get; set; }
        public string SourceTitle { get; set; }
        public AudioQualityModel Quality { get; set; }
        public DateTime Date { get; set; }
        public Album Album { get; set; }
        public Artist Artist { get; set; }
        public AlbumHistoryEventType EventType { get; set; }
        public Dictionary<string, string> Data { get; set; }
        public string DownloadId { get; set; }

        // Music has no languages. The shared History table requires the column and, on SQLite,
        // gives it no default, so every row writes an empty list.
        public List<Language> Languages { get; set; }
    }

    // Lidarr's values, so the importer and the Lidarr v1 API map one to one. They are stored
    // beside the other modules' values, told apart by MediaKind.
    public enum AlbumHistoryEventType
    {
        Unknown = 0,
        Grabbed = 1,
        ArtistFolderImported = 2,
        TrackFileImported = 3,
        DownloadFailed = 4,
        TrackFileDeleted = 5,
        TrackFileRenamed = 6,
        AlbumImportIncomplete = 7,
        DownloadImported = 8,
        TrackFileRetagged = 9,
        DownloadIgnored = 10
    }
}
