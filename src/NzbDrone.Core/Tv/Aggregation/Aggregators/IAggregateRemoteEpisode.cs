using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Tv.Aggregation.Aggregators
{
    public interface IAggregateRemoteEpisode
    {
        RemoteEpisode Aggregate(RemoteEpisode remoteEpisode);
    }
}
