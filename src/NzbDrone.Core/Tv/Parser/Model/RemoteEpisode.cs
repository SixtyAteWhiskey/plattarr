using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv.DataAugmentation.Scene;

namespace NzbDrone.Core.Tv.Parser.Model
{
    // Sonarr's RemoteEpisode on the shared RemoteRelease base, as RemoteMovie is for Movies.
    public class RemoteEpisode : RemoteRelease
    {
        public RemoteEpisode()
        {
            Episodes = new List<Episode>();
        }

        public ParsedEpisodeInfo ParsedEpisodeInfo { get; set; }
        public SceneMapping SceneMapping { get; set; }
        public int? MappedSeasonNumber { get; set; }
        public Series Series { get; set; }
        public List<Episode> Episodes { get; set; }
        public bool EpisodeRequested { get; set; }
        public SeriesMatchType SeriesMatchType { get; set; }

        [JsonIgnore]
        public override MediaKind Kind => MediaKind.Tv;

        [JsonIgnore]
        public override IMediaParent Parent => Series;

        [JsonIgnore]
        public override IReadOnlyList<int> ItemIds => Episodes == null ? Array.Empty<int>() : Episodes.Select(e => e.Id).ToList();

        [JsonIgnore]
        public override bool IsRecent => Episodes != null && Episodes.Any(e => e.AirDateUtc >= DateTime.UtcNow.Date.AddDays(-14));

        [JsonIgnore]
        public override QualityModel Quality => ParsedEpisodeInfo?.Quality ?? new QualityModel(Qualities.Quality.Unknown);

        [JsonIgnore]
        public override string ReleaseGroup => ParsedEpisodeInfo?.ReleaseGroup;

        [JsonIgnore]
        public override int? Year => Series?.Year;

        [JsonIgnore]
        public override string Studio => Series?.Network;
    }
}
