using System.Collections.Generic;

namespace Radarr.Api.V3.Music.Artist
{
    public class ArtistEditorDeleteResource
    {
        public List<int> ArtistIds { get; set; }
        public bool DeleteFiles { get; set; }
    }
}
