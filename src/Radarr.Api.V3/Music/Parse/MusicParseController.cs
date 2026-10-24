using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Music.Aggregation;
using NzbDrone.Core.Music.CustomFormats;
using NzbDrone.Core.Music.Parser;
using Radarr.Api.V3.CustomFormats;
using Radarr.Api.V3.Music.Albums;
using Radarr.Api.V3.Music.Artist;
using Radarr.Http;
using MusicParser = NzbDrone.Core.Music.Parser.Parser;

namespace Radarr.Api.V3.Music.Parse
{
    // Lidarr's parse controller. The movie controller owns /parse, so title parsing for albums is
    // served beside it at /parse/music.
    [V3ApiController("parse/music")]
    public class MusicParseController : Controller
    {
        private readonly IParsingService _parsingService;
        private readonly IRemoteAlbumAggregationService _aggregationService;
        private readonly IMusicCustomFormatCalculationService _formatCalculator;

        public MusicParseController(IParsingService parsingService,
                                    IRemoteAlbumAggregationService aggregationService,
                                    IMusicCustomFormatCalculationService formatCalculator)
        {
            _parsingService = parsingService;
            _aggregationService = aggregationService;
            _formatCalculator = formatCalculator;
        }

        [HttpGet]
        [Produces("application/json")]
        public MusicParseResource Parse(string title)
        {
            if (title.IsNullOrWhiteSpace())
            {
                return null;
            }

            var parsedAlbumInfo = MusicParser.ParseAlbumTitle(title);

            if (parsedAlbumInfo == null)
            {
                return new MusicParseResource
                {
                    Title = title
                };
            }

            var remoteAlbum = _parsingService.Map(parsedAlbumInfo);

            if (remoteAlbum != null)
            {
                _aggregationService.Augment(remoteAlbum);

                remoteAlbum.CustomFormats = _formatCalculator.ParseCustomFormat(remoteAlbum, 0);
                remoteAlbum.CustomFormatScore = remoteAlbum.Artist?.QualityProfile?.Value.CalculateCustomFormatScore(remoteAlbum.CustomFormats) ?? 0;

                return new MusicParseResource
                {
                    Title = title,
                    ParsedAlbumInfo = remoteAlbum.ParsedAlbumInfo,
                    Artist = remoteAlbum.Artist.ToResource(),
                    Albums = remoteAlbum.Albums.ToResource(),
                    CustomFormats = remoteAlbum.CustomFormats?.ToResource(false),
                    CustomFormatScore = remoteAlbum.CustomFormatScore
                };
            }

            return new MusicParseResource
            {
                Title = title,
                ParsedAlbumInfo = parsedAlbumInfo
            };
        }
    }
}
