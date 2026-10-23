using System.Collections.Generic;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv.Parser.Model;
using Radarr.Api.V3.CustomFormats;
using Radarr.Api.V3.Tv.Episodes;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Tv.ManualImport
{
    public class TvManualImportReprocessResource : RestResource
    {
        public string Path { get; set; }
        public int SeriesId { get; set; }
        public int? SeasonNumber { get; set; }
        public List<EpisodeResource> Episodes { get; set; }
        public List<int> EpisodeIds { get; set; }
        public QualityModel Quality { get; set; }
        public List<Language> Languages { get; set; }
        public string ReleaseGroup { get; set; }
        public string DownloadId { get; set; }
        public List<CustomFormatResource> CustomFormats { get; set; }
        public int CustomFormatScore { get; set; }
        public int IndexerFlags { get; set; }
        public ReleaseType ReleaseType { get; set; }
        public IEnumerable<TvImportRejectionResource> Rejections { get; set; }
    }
}
