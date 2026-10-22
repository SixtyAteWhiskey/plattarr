using NzbDrone.Core.Download;
using NzbDrone.Core.Movies.Parser.Model;

namespace NzbDrone.Core.Movies.MediaFiles.MovieImport.Aggregation.Aggregators
{
    public interface IAggregateLocalMovie
    {
        int Order { get; }

        LocalMovie Aggregate(LocalMovie localMovie, DownloadClientItem downloadClientItem);
    }
}
