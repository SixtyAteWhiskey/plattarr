using System.Collections.Generic;
using System.Text.Json.Serialization;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser.Model;

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

        public override string ToString()
        {
            return Release?.Title;
        }
    }
}
