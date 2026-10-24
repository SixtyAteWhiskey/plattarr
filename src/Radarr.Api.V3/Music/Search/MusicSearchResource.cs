using Radarr.Api.V3.Music.Albums;
using Radarr.Api.V3.Music.Artist;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Music.Search
{
    // Lidarr's SearchResource: one entry of the combined artist and album lookup. Exactly one of Artist
    // and Album is set.
    public class MusicSearchResource : RestResource
    {
        public string ForeignId { get; set; }
        public ArtistResource Artist { get; set; }
        public AlbumResource Album { get; set; }
    }
}
