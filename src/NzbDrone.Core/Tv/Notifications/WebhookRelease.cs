using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Tv.Notifications
{
    public class WebhookRelease
    {
        public WebhookRelease()
        {
        }

        public WebhookRelease(QualityModel quality, RemoteEpisode remoteEpisode)
        {
            Quality = quality.Quality.Name;
            QualityVersion = quality.Revision.Version;
            ReleaseGroup = remoteEpisode.ParsedEpisodeInfo.ReleaseGroup;
            ReleaseTitle = remoteEpisode.Release.Title;
            Indexer = remoteEpisode.Release.Indexer;
            Size = remoteEpisode.Release.Size.GetValueOrDefault();
            CustomFormats = remoteEpisode.CustomFormats?.Select(x => x.Name).ToList();
            CustomFormatScore = remoteEpisode.CustomFormatScore;
            Languages = remoteEpisode.Languages;
            IndexerFlags = Enum.GetValues(typeof(IndexerFlags)).Cast<IndexerFlags>().Where(f => (remoteEpisode.Release.ToIndexerFlags() & f) == f).Select(f => f.ToString()).ToList();
        }

        public string Quality { get; set; }
        public int QualityVersion { get; set; }
        public string ReleaseGroup { get; set; }
        public string ReleaseTitle { get; set; }
        public string Indexer { get; set; }
        public long Size { get; set; }
        public int CustomFormatScore { get; set; }
        public List<string> CustomFormats { get; set; }
        public List<Language> Languages { get; set; }
        public List<string> IndexerFlags { get; set; }
    }
}
