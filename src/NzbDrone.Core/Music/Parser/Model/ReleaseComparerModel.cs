using System;
using System.Collections.Generic;
using NzbDrone.Core.Music.Blocklisting;
using NzbDrone.Core.Music.History;

namespace NzbDrone.Core.Music.Parser.Model;

// What the blocklist check compares a release against: a blocklist row or a history row.
public class ReleaseComparerModel
{
    public string Title { get; set; }
    public string TorrentInfoHash { get; set; }
    public DateTime? PublishedDate { get; set; }
    public string Indexer { get; set; }
    public long Size { get; set; }

    public ReleaseComparerModel(Blocklist blocklist)
    {
        Title = blocklist.SourceTitle;
        TorrentInfoHash = blocklist.TorrentInfoHash;
        PublishedDate = blocklist.PublishedDate;
        Indexer = blocklist.Indexer;
        Size = blocklist.Size ?? 0;
    }

    public ReleaseComparerModel(AlbumHistory history)
    {
        Title = history.SourceTitle;
        PublishedDate = history.Date;
        Indexer = history.Data.GetValueOrDefault("indexer");
        Size = long.Parse(history.Data.GetValueOrDefault("size", "0"));
    }
}
