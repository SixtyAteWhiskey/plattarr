using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Music.Parser.Model
{
    // Lidarr's RemoteAlbum on the shared RemoteRelease base, as RemoteMovie and RemoteEpisode are for
    // Movies and Tv. The parent is the artist and the items are the albums the release covers.
    public class RemoteAlbum : RemoteRelease
    {
        public RemoteAlbum()
        {
            Albums = new List<Album>();
        }

        public ParsedAlbumInfo ParsedAlbumInfo { get; set; }
        public Artist Artist { get; set; }
        public List<Album> Albums { get; set; }

        [JsonIgnore]
        public override MediaKind Kind => MediaKind.Music;

        [JsonIgnore]
        public override IMediaParent Parent => Artist;

        [JsonIgnore]
        public override IReadOnlyList<int> ItemIds => Albums == null ? Array.Empty<int>() : Albums.Select(a => a.Id).ToList();

        [JsonIgnore]
        public override bool IsRecent => Albums != null && Albums.Any(a => a.ReleaseDate >= DateTime.UtcNow.Date.AddDays(-14));

        // Core types carry a video QualityModel; a Music release shows its audio quality as a display
        // copy (AudioQualityModel.ToDisplayModel). Music's own code reads ParsedAlbumInfo.Quality.
        [JsonIgnore]
        public override QualityModel Quality => (ParsedAlbumInfo?.Quality ?? new AudioQualityModel(AudioQuality.Unknown)).ToDisplayModel();

        [JsonIgnore]
        public override string ReleaseGroup => ParsedAlbumInfo?.ReleaseGroup;

        [JsonIgnore]
        public override int? Year => ParsedAlbumInfo?.ReleaseYear ?? Albums?.FirstOrDefault()?.ReleaseDate?.Year;
    }
}
