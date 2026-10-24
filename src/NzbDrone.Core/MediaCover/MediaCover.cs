using Equ;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.MediaCover
{
    public enum MediaCoverTypes
    {
        Unknown = 0,
        Poster = 1,
        Banner = 2,
        Fanart = 3,
        Screenshot = 4,
        Headshot = 5,
        Clearlogo = 6,

        // Music's image types (Lidarr's Cover, Disc and Logo). Covers are stored by name, so
        // these take the next free numbers rather than Lidarr's.
        Cover = 7,
        Disc = 8,
        Logo = 9
    }

    public class MediaCover : MemberwiseEquatable<MediaCover>, IEmbeddedDocument
    {
        public MediaCoverTypes CoverType { get; set; }
        public string Url { get; set; }
        public string RemoteUrl { get; set; }

        public MediaCover()
        {
        }

        public MediaCover(MediaCoverTypes coverType, string remoteUrl)
        {
            CoverType = coverType;
            RemoteUrl = remoteUrl;
        }
    }
}
