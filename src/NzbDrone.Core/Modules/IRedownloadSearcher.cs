using NzbDrone.Core.Download;

namespace NzbDrone.Core.Modules
{
    // Contract 5, redownload half (porting step 9k). After a download fails, the core checks the
    // user's settings (skip requested, auto redownload on, interactive search allowed) and asks
    // the module to search again, because only the module knows what a search for its media looks
    // like (a movie search, an episode or season search, an album search).
    public interface IRedownloadSearcher
    {
        MediaKind Kind { get; }

        // Queues a new search for what the failed download was for.
        void Search(DownloadFailedEvent failure);
    }
}
