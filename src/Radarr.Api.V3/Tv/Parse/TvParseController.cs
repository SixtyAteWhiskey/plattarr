using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Tv.Aggregation;
using NzbDrone.Core.Tv.CustomFormats;
using NzbDrone.Core.Tv.Parser;
using Radarr.Api.V3.CustomFormats;
using Radarr.Api.V3.Tv.Episodes;
using Radarr.Api.V3.Tv.Series;
using Radarr.Http;
using TvParser = NzbDrone.Core.Tv.Parser.Parser;

namespace Radarr.Api.V3.Tv.Parse
{
    // Sonarr's parse controller. The movie controller owns /parse, so title and path parsing for
    // series is served beside it at /parse/tv.
    [V3ApiController("parse/tv")]
    public class TvParseController : Controller
    {
        private readonly IParsingService _parsingService;
        private readonly IRemoteEpisodeAggregationService _aggregationService;
        private readonly ITvCustomFormatCalculationService _formatCalculator;

        public TvParseController(IParsingService parsingService,
                                 IRemoteEpisodeAggregationService aggregationService,
                                 ITvCustomFormatCalculationService formatCalculator)
        {
            _parsingService = parsingService;
            _aggregationService = aggregationService;
            _formatCalculator = formatCalculator;
        }

        [HttpGet]
        [Produces("application/json")]
        public TvParseResource Parse(string title, string path)
        {
            if (title.IsNullOrWhiteSpace())
            {
                return null;
            }

            var parsedEpisodeInfo = path.IsNotNullOrWhiteSpace() ? TvParser.ParsePath(path) : TvParser.ParseTitle(title);

            // Try to parse a season title if previous parsing failed and we're not dealing with a path.
            if (parsedEpisodeInfo == null && path.IsNullOrWhiteSpace())
            {
                parsedEpisodeInfo = TvParser.ParseSeasonTitle(title);
            }

            if (parsedEpisodeInfo == null)
            {
                return new TvParseResource
                {
                    Title = title
                };
            }

            var remoteEpisode = _parsingService.Map(parsedEpisodeInfo, 0, 0, null);

            if (remoteEpisode != null)
            {
                _aggregationService.Augment(remoteEpisode);

                remoteEpisode.CustomFormats = _formatCalculator.ParseCustomFormat(remoteEpisode, 0);
                remoteEpisode.CustomFormatScore = remoteEpisode.Series?.QualityProfile?.Value.CalculateCustomFormatScore(remoteEpisode.CustomFormats) ?? 0;

                return new TvParseResource
                {
                    Title = title,
                    ParsedEpisodeInfo = remoteEpisode.ParsedEpisodeInfo.ToResource(),
                    Series = remoteEpisode.Series.ToResource(),
                    Episodes = remoteEpisode.Episodes.ToResource(),
                    Languages = remoteEpisode.Languages,
                    CustomFormats = remoteEpisode.CustomFormats?.ToResource(false),
                    CustomFormatScore = remoteEpisode.CustomFormatScore
                };
            }

            return new TvParseResource
            {
                Title = title,
                ParsedEpisodeInfo = parsedEpisodeInfo.ToResource()
            };
        }
    }
}
