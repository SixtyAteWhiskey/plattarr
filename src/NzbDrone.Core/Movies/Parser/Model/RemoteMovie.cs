using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Movies.Parser.Model
{
    public class RemoteMovie : RemoteRelease
    {
        public ParsedMovieInfo ParsedMovieInfo { get; set; }
        public MovieMatchType MovieMatchType { get; set; }
        public Movie Movie { get; set; }
        public bool MovieRequested { get; set; }

        [JsonIgnore]
        public override MediaKind Kind => MediaKind.Movies;

        [JsonIgnore]
        public override IMediaParent Parent => Movie;

        [JsonIgnore]
        public override IReadOnlyList<int> ItemIds => Movie == null ? Array.Empty<int>() : new[] { Movie.Id };

        [JsonIgnore]
        public override bool IsRecent => Movie?.MovieMetadata?.Value?.IsRecentMovie ?? false;

        [JsonIgnore]
        public override QualityModel Quality => ParsedMovieInfo?.Quality ?? new QualityModel(Qualities.Quality.Unknown);

        [JsonIgnore]
        public override string ReleaseGroup => ParsedMovieInfo?.ReleaseGroup;

        [JsonIgnore]
        public override int? Year => Movie?.Year;

        [JsonIgnore]
        public override string Collection => Movie?.MovieMetadata?.Value?.CollectionTitle;

        [JsonIgnore]
        public override string Studio => Movie?.MovieMetadata?.Value?.Studio;
    }
}
