using System.Collections.Generic;
using NzbDrone.Core.Languages;
using Radarr.Api.V3.CustomFormats;
using Radarr.Api.V3.Tv.Episodes;
using Radarr.Api.V3.Tv.Series;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Tv.Parse
{
    public class TvParseResource : RestResource
    {
        public string Title { get; set; }
        public TvParsedEpisodeInfoResource ParsedEpisodeInfo { get; set; }
        public SeriesResource Series { get; set; }
        public List<EpisodeResource> Episodes { get; set; }
        public List<Language> Languages { get; set; }
        public List<CustomFormatResource> CustomFormats { get; set; }
        public int CustomFormatScore { get; set; }
    }
}
