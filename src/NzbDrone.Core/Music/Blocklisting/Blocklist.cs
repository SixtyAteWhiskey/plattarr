using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Music.Blocklisting
{
    // The Music module's view of the shared Blocklist table. ParentId is the artist and ItemIds
    // the albums the release covered (migration 004 adds ItemIds).
    public class Blocklist : ModelBase, IMediaKindRecord
    {
        public Blocklist()
        {
            Languages = new List<Language>();
        }

        public MediaKind MediaKind { get; set; }
        public int ParentId { get; set; }
        public Artist Artist { get; set; }
        public List<int> ItemIds { get; set; }
        public string SourceTitle { get; set; }
        public AudioQualityModel Quality { get; set; }
        public DateTime Date { get; set; }
        public DateTime? PublishedDate { get; set; }
        public long? Size { get; set; }
        public DownloadProtocol Protocol { get; set; }
        public string Indexer { get; set; }
        public IndexerFlags IndexerFlags { get; set; }
        public string Message { get; set; }
        public string TorrentInfoHash { get; set; }

        // Music has no languages. The shared Blocklist table requires the column and, on SQLite,
        // gives it no default, so every row writes an empty list.
        public List<Language> Languages { get; set; }
    }
}
