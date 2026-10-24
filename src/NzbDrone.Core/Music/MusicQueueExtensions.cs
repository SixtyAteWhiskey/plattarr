using System.Linq;
using NzbDrone.Core.Music.Parser.Model;

namespace NzbDrone.Core.Music
{
    // The core queue carries the module's match as a RemoteRelease; Music code reads it as a RemoteAlbum.
    public static class MusicQueueExtensions
    {
        public static RemoteAlbum GetRemoteAlbum(this Queue.Queue queue)
        {
            return queue.RemoteRelease as RemoteAlbum;
        }

        public static Artist GetArtist(this Queue.Queue queue)
        {
            return queue.GetRemoteAlbum()?.Artist;
        }

        public static Album GetAlbum(this Queue.Queue queue)
        {
            return queue.GetRemoteAlbum()?.Albums?.FirstOrDefault();
        }
    }
}
