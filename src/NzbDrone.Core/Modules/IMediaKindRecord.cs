namespace NzbDrone.Core.Modules
{
    // A row in a table every media type shares (History, DownloadHistory,
    // Blocklist, PendingReleases). The kind says which module owns the row.
    public interface IMediaKindRecord
    {
        MediaKind MediaKind { get; set; }
    }
}
