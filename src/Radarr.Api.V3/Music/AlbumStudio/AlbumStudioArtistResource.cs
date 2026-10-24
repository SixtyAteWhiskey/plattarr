using System.Collections.Generic;
using Radarr.Api.V3.Music.Albums;

namespace Radarr.Api.V3.Music.AlbumStudio
{
    public class AlbumStudioArtistResource
    {
        public int Id { get; set; }
        public bool? Monitored { get; set; }
        public List<AlbumResource> Albums { get; set; }
    }
}
