using System.Collections.Generic;
using System.Text.Json.Serialization;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Modules
{
    // A release matched to a library entity by a module: RemoteMovie,
    // RemoteEpisode, or RemoteAlbum. See docs/module-interface.md.
    public abstract class RemoteRelease
    {
        protected RemoteRelease()
        {
            CustomFormats = new List<CustomFormat>();
            Languages = new List<Language>();
        }

        public ReleaseInfo Release { get; set; }
        public bool DownloadAllowed { get; set; }
        public TorrentSeedConfiguration SeedConfiguration { get; set; }
        public List<CustomFormat> CustomFormats { get; set; }
        public int CustomFormatScore { get; set; }
        public List<Language> Languages { get; set; }
        public ReleaseSourceType ReleaseSource { get; set; }

        [JsonIgnore]
        public abstract MediaKind Kind { get; }

        [JsonIgnore]
        public abstract IMediaParent Parent { get; }

        [JsonIgnore]
        public abstract IReadOnlyList<int> ItemIds { get; }

        [JsonIgnore]
        public abstract bool IsRecent { get; }

        // The quality the module's parser read from the release title (Unknown when it found none).
        [JsonIgnore]
        public abstract QualityModel Quality { get; }

        // The values download clients turn into client-side tags (Flood's additional tags). The
        // release group is whatever the module's parser read from the title.
        [JsonIgnore]
        public abstract string ReleaseGroup { get; }

        // Release year of the movie, series or album. Null when unknown.
        [JsonIgnore]
        public abstract int? Year { get; }

        // Movie collection or series group. Null for media with none.
        [JsonIgnore]
        public virtual string Collection => null;

        // Studio, network or label. Null when unknown.
        [JsonIgnore]
        public virtual string Studio => null;

        public override string ToString()
        {
            return Release?.Title;
        }
    }
}
