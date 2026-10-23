using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Movies.Extras.Files;
using NzbDrone.Core.Tv.Extras.Metadata.Files;
using NzbDrone.Core.Tv.Extras.Others;
using NzbDrone.Core.Tv.Extras.Subtitles;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Tv.ExtraFiles
{
    public class TvExtraFileResource : RestResource
    {
        public int SeriesId { get; set; }
        public int? SeasonNumber { get; set; }
        public int? EpisodeFileId { get; set; }
        public string RelativePath { get; set; }
        public string Extension { get; set; }
        public List<string> LanguageTags { get; set; }
        public string Title { get; set; }

        // The enum is the one Radarr's endpoint uses (Subtitle, Metadata, Other), so a client reads both alike.
        public ExtraFileType Type { get; set; }
    }

    public static class TvExtraFileResourceMapper
    {
        public static TvExtraFileResource ToResource(this MetadataFile model)
        {
            if (model == null)
            {
                return null;
            }

            return new TvExtraFileResource
            {
                Id = model.Id,
                SeriesId = model.SeriesId,
                SeasonNumber = model.SeasonNumber,
                EpisodeFileId = model.EpisodeFileId,
                RelativePath = model.RelativePath,
                Extension = model.Extension,
                Type = ExtraFileType.Metadata
            };
        }

        public static TvExtraFileResource ToResource(this SubtitleFile model)
        {
            if (model == null)
            {
                return null;
            }

            return new TvExtraFileResource
            {
                Id = model.Id,
                SeriesId = model.SeriesId,
                SeasonNumber = model.SeasonNumber,
                EpisodeFileId = model.EpisodeFileId,
                RelativePath = model.RelativePath,
                Extension = model.Extension,
                LanguageTags = model.LanguageTags,
                Title = model.Title,
                Type = ExtraFileType.Subtitle
            };
        }

        public static TvExtraFileResource ToResource(this OtherExtraFile model)
        {
            if (model == null)
            {
                return null;
            }

            return new TvExtraFileResource
            {
                Id = model.Id,
                SeriesId = model.SeriesId,
                SeasonNumber = model.SeasonNumber,
                EpisodeFileId = model.EpisodeFileId,
                RelativePath = model.RelativePath,
                Extension = model.Extension,
                Type = ExtraFileType.Other
            };
        }

        public static List<TvExtraFileResource> ToResource(this IEnumerable<SubtitleFile> files)
        {
            return files.Select(ToResource).ToList();
        }

        public static List<TvExtraFileResource> ToResource(this IEnumerable<MetadataFile> files)
        {
            return files.Select(ToResource).ToList();
        }

        public static List<TvExtraFileResource> ToResource(this IEnumerable<OtherExtraFile> files)
        {
            return files.Select(ToResource).ToList();
        }
    }
}
