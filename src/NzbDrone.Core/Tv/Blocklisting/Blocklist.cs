using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Tv.Blocklisting
{
    // The TV module's view of the shared Blocklist table. ParentId is the series, ItemIds the
    // episodes the release covered, ReleaseType whether it was an episode, a multi-episode or a
    // season pack (migration 004 adds ItemIds and ReleaseType). Sonarr's Source column (what
    // reported the failure) is not carried: Radarr never stored it and the core failed-download
    // event has no such field.
    public class Blocklist : ModelBase, IMediaKindRecord
    {
        public MediaKind MediaKind { get; set; }
        public int ParentId { get; set; }
        public Series Series { get; set; }
        public List<int> ItemIds { get; set; }
        public string SourceTitle { get; set; }
        public QualityModel Quality { get; set; }
        public DateTime Date { get; set; }
        public DateTime? PublishedDate { get; set; }
        public long? Size { get; set; }
        public DownloadProtocol Protocol { get; set; }
        public string Indexer { get; set; }
        public IndexerFlags IndexerFlags { get; set; }
        public ReleaseType ReleaseType { get; set; }
        public string Message { get; set; }
        public string TorrentInfoHash { get; set; }
        public List<Language> Languages { get; set; }
    }
}
