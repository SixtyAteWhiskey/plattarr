using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Crypto;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv.MediaFiles.EpisodeImport;
using NzbDrone.Core.Tv.MediaFiles.EpisodeImport.Manual;
using NzbDrone.Core.Tv.Parser.Model;
using Radarr.Api.V3.CustomFormats;
using Radarr.Api.V3.Tv.Episodes;
using Radarr.Api.V3.Tv.Series;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Tv.ManualImport
{
    public class TvManualImportResource : RestResource
    {
        public string Path { get; set; }
        public string RelativePath { get; set; }
        public string FolderName { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
        public SeriesResource Series { get; set; }
        public int? SeasonNumber { get; set; }
        public List<EpisodeResource> Episodes { get; set; }
        public int? EpisodeFileId { get; set; }
        public string ReleaseGroup { get; set; }
        public QualityModel Quality { get; set; }
        public List<Language> Languages { get; set; }
        public int QualityWeight { get; set; }
        public string DownloadId { get; set; }
        public List<CustomFormatResource> CustomFormats { get; set; }
        public int CustomFormatScore { get; set; }
        public int IndexerFlags { get; set; }
        public ReleaseType ReleaseType { get; set; }
        public IEnumerable<TvImportRejectionResource> Rejections { get; set; }
    }

    public static class TvManualImportResourceMapper
    {
        public static TvManualImportResource ToResource(this ManualImportItem model)
        {
            if (model == null)
            {
                return null;
            }

            var customFormats = model.CustomFormats;
            var customFormatScore = model.Series?.QualityProfile?.Value?.CalculateCustomFormatScore(customFormats) ?? 0;

            return new TvManualImportResource
            {
                Id = HashConverter.GetHashInt31(model.Path),
                Path = model.Path,
                RelativePath = model.RelativePath,
                FolderName = model.FolderName,
                Name = model.Name,
                Size = model.Size,
                Series = model.Series.ToResource(),
                SeasonNumber = model.SeasonNumber,
                Episodes = model.Episodes.ToResource(),
                EpisodeFileId = model.EpisodeFileId,
                ReleaseGroup = model.ReleaseGroup,
                Quality = model.Quality,
                Languages = model.Languages,
                CustomFormats = customFormats.ToResource(false),
                CustomFormatScore = customFormatScore,

                // QualityWeight
                DownloadId = model.DownloadId,
                IndexerFlags = model.IndexerFlags,
                ReleaseType = model.ReleaseType,
                Rejections = model.Rejections.Select(r => r.ToResource())
            };
        }

        public static List<TvManualImportResource> ToResource(this IEnumerable<ManualImportItem> models)
        {
            return models.Select(ToResource).ToList();
        }
    }

    public class TvImportRejectionResource
    {
        public string Reason { get; set; }
        public NzbDrone.Core.DecisionEngine.RejectionType Type { get; set; }
    }

    public static class TvImportRejectionResourceMapper
    {
        public static TvImportRejectionResource ToResource(this ImportRejection rejection)
        {
            if (rejection == null)
            {
                return null;
            }

            return new TvImportRejectionResource
            {
                Reason = rejection.Message,
                Type = rejection.Type
            };
        }
    }
}
