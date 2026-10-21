namespace NzbDrone.Core.Modules
{
    // Contract 6 in docs/module-interface.md. A media module answers blocklist questions about its
    // own releases, so a download client can refuse a torrent whose hash was blocklisted without
    // knowing which media type it belongs to.
    public interface IMediaBlocklist
    {
        MediaKind Kind { get; }

        bool IsTorrentHashBlocklisted(RemoteRelease release, string hash);
    }
}
