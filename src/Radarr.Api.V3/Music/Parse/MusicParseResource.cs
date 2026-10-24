using System.Collections.Generic;
using NzbDrone.Core.Music.Parser.Model;
using Radarr.Api.V3.CustomFormats;
using Radarr.Api.V3.Music.Albums;
using Radarr.Api.V3.Music.Artist;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Music.Parse
{
    public class MusicParseResource : RestResource
    {
        public string Title { get; set; }
        public ParsedAlbumInfo ParsedAlbumInfo { get; set; }
        public ArtistResource Artist { get; set; }
        public List<AlbumResource> Albums { get; set; }
        public List<CustomFormatResource> CustomFormats { get; set; }
        public int CustomFormatScore { get; set; }
    }
}
