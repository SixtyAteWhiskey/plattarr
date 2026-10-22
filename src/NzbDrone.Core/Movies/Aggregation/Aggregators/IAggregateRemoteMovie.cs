using NzbDrone.Core.Movies.Parser.Model;

namespace NzbDrone.Core.Movies.Aggregation.Aggregators
{
    public interface IAggregateRemoteMovie
    {
        RemoteMovie Aggregate(RemoteMovie remoteMovie);
    }
}
