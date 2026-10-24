namespace NzbDrone.Core.Music.Search
{
    // Lidarr's ArtistSearchCriteria: a search for an artist's albums.
    public class ArtistSearchRequest : MusicSearchRequest
    {
        public override string ToString()
        {
            return $"[{Artist?.Name}]";
        }
    }
}
