using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Tv.Extras.Files;
using NzbDrone.Core.Tv.Extras.Metadata.Files;
using NzbDrone.Core.Tv.Extras.Others;
using NzbDrone.Core.Tv.Extras.Subtitles;
using Radarr.Http;

namespace Radarr.Api.V3.Tv.ExtraFiles
{
    // Sonarr has no extra file endpoint; this is the series version of Radarr's `extrafile`, which the
    // movie controller owns, so it is served beside it at /extrafile/tv. Subtitles, then metadata, then
    // other files, each ordered by path.
    [V3ApiController("extrafile/tv")]
    public class TvExtraFileController : Controller
    {
        private readonly IExtraFileService<SubtitleFile> _subtitleFileService;
        private readonly IExtraFileService<MetadataFile> _metadataFileService;
        private readonly IExtraFileService<OtherExtraFile> _otherFileService;

        public TvExtraFileController(IExtraFileService<SubtitleFile> subtitleFileService,
                                     IExtraFileService<MetadataFile> metadataFileService,
                                     IExtraFileService<OtherExtraFile> otherExtraFileService)
        {
            _subtitleFileService = subtitleFileService;
            _metadataFileService = metadataFileService;
            _otherFileService = otherExtraFileService;
        }

        [HttpGet]
        [Produces("application/json")]
        public Ok<List<TvExtraFileResource>> GetFiles(int seriesId)
        {
            var extraFiles = new List<TvExtraFileResource>();

            extraFiles.AddRange(_subtitleFileService.GetFilesBySeries(seriesId).OrderBy(f => f.RelativePath).ToResource());
            extraFiles.AddRange(_metadataFileService.GetFilesBySeries(seriesId).OrderBy(f => f.RelativePath).ToResource());
            extraFiles.AddRange(_otherFileService.GetFilesBySeries(seriesId).OrderBy(f => f.RelativePath).ToResource());

            return TypedResults.Ok(extraFiles);
        }
    }
}
