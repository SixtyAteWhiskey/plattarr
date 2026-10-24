using NzbDrone.Core.Music.Parser.Model;

namespace NzbDrone.Core.Music.Aggregation.Aggregators
{
    public interface IAggregateRemoteAlbum
    {
        RemoteAlbum Aggregate(RemoteAlbum remoteAlbum);
    }
}
