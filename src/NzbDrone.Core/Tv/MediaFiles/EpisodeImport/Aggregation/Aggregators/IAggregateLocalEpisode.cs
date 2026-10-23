using NzbDrone.Core.Download;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Tv.MediaFiles.EpisodeImport.Aggregation.Aggregators
{
    public interface IAggregateLocalEpisode
    {
        int Order { get; }
        LocalEpisode Aggregate(LocalEpisode localEpisode, DownloadClientItem downloadClientItem);
    }
}
