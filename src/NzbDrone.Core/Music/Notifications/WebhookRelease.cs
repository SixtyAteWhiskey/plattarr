using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Music.Notifications
{
    public class WebhookRelease
    {
        public WebhookRelease()
        {
        }

        public WebhookRelease(AudioQualityModel quality, RemoteAlbum remoteAlbum)
        {
            Quality = quality.Quality.Name;
            QualityVersion = quality.Revision.Version;
            ReleaseGroup = remoteAlbum.ParsedAlbumInfo.ReleaseGroup;
            ReleaseTitle = remoteAlbum.Release.Title;
            Indexer = remoteAlbum.Release.Indexer;
            Size = remoteAlbum.Release.Size.GetValueOrDefault();
            CustomFormats = remoteAlbum.CustomFormats?.Select(x => x.Name).ToList();
            CustomFormatScore = remoteAlbum.CustomFormatScore;
        }

        public string Quality { get; set; }
        public int QualityVersion { get; set; }
        public string ReleaseGroup { get; set; }
        public string ReleaseTitle { get; set; }
        public string Indexer { get; set; }
        public long Size { get; set; }
        public int CustomFormatScore { get; set; }
        public List<string> CustomFormats { get; set; }
    }
}
