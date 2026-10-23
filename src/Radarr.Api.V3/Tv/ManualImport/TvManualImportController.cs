using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv.MediaFiles.EpisodeImport.Manual;
using Radarr.Api.V3.CustomFormats;
using Radarr.Api.V3.Tv.Episodes;
using Radarr.Http;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Tv.ManualImport
{
    // Sonarr's manual import controller. The movie controller owns /manualimport, so the series version
    // is served beside it at /manualimport/tv. Running the import is the TvManualImport command.
    [V3ApiController("manualimport/tv")]
    public class TvManualImportController : Controller
    {
        private readonly IManualImportService _manualImportService;

        public TvManualImportController(IManualImportService manualImportService)
        {
            _manualImportService = manualImportService;
        }

        [HttpGet]
        [Produces("application/json")]
        public List<TvManualImportResource> GetMediaFiles(string folder, string downloadId, int? seriesId, int? seasonNumber, bool filterExistingFiles = true)
        {
            if (seriesId.HasValue && downloadId.IsNullOrWhiteSpace())
            {
                return _manualImportService.GetMediaFiles(seriesId.Value, seasonNumber).ToResource().Select(AddQualityWeight).ToList();
            }

            return _manualImportService.GetMediaFiles(folder, downloadId, seriesId, filterExistingFiles).ToResource().Select(AddQualityWeight).ToList();
        }

        [HttpPost]
        [Consumes("application/json")]
        public object ReprocessItems([FromBody] List<TvManualImportReprocessResource> items)
        {
            if (items is { Count: 0 })
            {
                throw new BadRequestException("items must be provided");
            }

            foreach (var item in items)
            {
                // A client that leaves these out means "work it out", as Unknown does.
                item.Quality ??= new QualityModel();
                item.Languages ??= new List<Language>();

                var processedItem = _manualImportService.ReprocessItem(item.Path, item.DownloadId, item.SeriesId, item.SeasonNumber, item.EpisodeIds ?? new List<int>(), item.ReleaseGroup, item.Quality, item.Languages, item.IndexerFlags, item.ReleaseType);

                item.SeasonNumber = processedItem.SeasonNumber;
                item.Episodes = processedItem.Episodes.ToResource();
                item.ReleaseType = processedItem.ReleaseType;
                item.IndexerFlags = processedItem.IndexerFlags;
                item.Rejections = processedItem.Rejections.Select(r => r.ToResource());
                item.CustomFormats = processedItem.CustomFormats.ToResource(false);
                item.CustomFormatScore = processedItem.CustomFormatScore;

                // Only set the language/quality if they're unknown and languages were returned.
                // Languages won't be returned when reprocessing if the season/episode isn't filled in yet and we don't want to return no languages to the client.
                if (item.Languages.Count <= 1 && (item.Languages.SingleOrDefault() ?? Language.Unknown) == Language.Unknown && processedItem.Languages.Any())
                {
                    item.Languages = processedItem.Languages;
                }

                if (item.Quality?.Quality == Quality.Unknown)
                {
                    item.Quality = processedItem.Quality;
                }

                if (item.ReleaseGroup.IsNullOrWhiteSpace())
                {
                    item.ReleaseGroup = processedItem.ReleaseGroup;
                }

                // Clear episode IDs in favour of the full episode
                item.EpisodeIds = null;
            }

            return items;
        }

        private TvManualImportResource AddQualityWeight(TvManualImportResource item)
        {
            if (item.Quality != null)
            {
                item.QualityWeight = Quality.DefaultQualityDefinitions.Single(q => q.Quality == item.Quality.Quality).Weight;
                item.QualityWeight += item.Quality.Revision.Real * 10;
                item.QualityWeight += item.Quality.Revision.Version;
            }

            return item;
        }
    }
}
