using NzbDrone.Core.Movies.Parser.Model;

namespace NzbDrone.Core.Movies
{
    // The core queue carries the module's match as a RemoteRelease; movie code reads it as a RemoteMovie.
    public static class MovieQueueExtensions
    {
        public static RemoteMovie GetRemoteMovie(this Queue.Queue queue)
        {
            return queue.RemoteRelease as RemoteMovie;
        }

        public static Movie GetMovie(this Queue.Queue queue)
        {
            return queue.GetRemoteMovie()?.Movie;
        }
    }
}
