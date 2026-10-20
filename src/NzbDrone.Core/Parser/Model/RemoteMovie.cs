using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;

namespace NzbDrone.Core.Parser.Model
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
    }

    public enum ReleaseSourceType
    {
        Unknown = 0,
        Rss = 1,
        Search = 2,
        UserInvokedSearch = 3,
        InteractiveSearch = 4,
        ReleasePush = 5
    }
}
