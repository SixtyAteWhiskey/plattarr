using System.Collections.Generic;
using NzbDrone.Core.Datastore.Converters;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv.MediaFiles.MediaInfo;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Tv.Upstream
{
    // The JSON columns of Sonarr's series tables, read with the converters the target's
    // repositories use (the ones TableMapping and TvTableMap register), so what a stage writes is
    // what the target reads back. Quality and language columns hold Sonarr IDs and are rewritten
    // through the catalog first. A document the target cannot read throws, and the stage skips
    // that one row as CORRUPT (SourceDocuments.IsBad), except where a stage says otherwise.
    public static class TvDocuments
    {
        private static readonly EmbeddedDocumentConverter<List<MediaCover.MediaCover>> ImagesConverter = new EmbeddedDocumentConverter<List<MediaCover.MediaCover>>();
        private static readonly EmbeddedDocumentConverter<List<string>> StringsConverter = new EmbeddedDocumentConverter<List<string>>();
        private static readonly EmbeddedDocumentConverter<HashSet<int>> IntSetConverter = new EmbeddedDocumentConverter<HashSet<int>>();
        private static readonly EmbeddedDocumentConverter<Ratings> RatingsConverter = new EmbeddedDocumentConverter<Ratings>();
        private static readonly EmbeddedDocumentConverter<List<Actor>> ActorsConverter = new EmbeddedDocumentConverter<List<Actor>>();
        private static readonly EmbeddedDocumentConverter<List<Season>> SeasonsConverter = new EmbeddedDocumentConverter<List<Season>>();
        private static readonly EmbeddedDocumentConverter<AddSeriesOptions> AddOptionsConverter = new EmbeddedDocumentConverter<AddSeriesOptions>();
        private static readonly EmbeddedDocumentConverter<MediaInfoModel> MediaInfoConverter = new EmbeddedDocumentConverter<MediaInfoModel>();
        private static readonly EmbeddedDocumentConverter<QualityModel> QualityConverter = new EmbeddedDocumentConverter<QualityModel>(new QualityIntConverter());
        private static readonly EmbeddedDocumentConverter<List<Language>> LanguagesConverter = new EmbeddedDocumentConverter<List<Language>>(new LanguageIntConverter());

        public static List<MediaCover.MediaCover> ReadImages(SourceRow row, string column)
        {
            return SourceDocuments.Parse(row, column, ImagesConverter) ?? new List<MediaCover.MediaCover>();
        }

        public static List<string> ReadStrings(SourceRow row, string column)
        {
            return SourceDocuments.Parse(row, column, StringsConverter) ?? new List<string>();
        }

        public static HashSet<int> ReadIntSet(SourceRow row, string column)
        {
            return SourceDocuments.Parse(row, column, IntSetConverter) ?? new HashSet<int>();
        }

        // Sonarr 4 writes no ratings for a series or episode TVDB has none for.
        public static Ratings ReadRatings(SourceRow row, string column)
        {
            return SourceDocuments.Parse(row, column, RatingsConverter) ?? new Ratings();
        }

        public static List<Actor> ReadActors(SourceRow row, string column)
        {
            return SourceDocuments.Parse(row, column, ActorsConverter) ?? new List<Actor>();
        }

        // Each season's number, monitored flag and images. Plattarr's season also has a title,
        // which the first refresh fills in.
        public static List<Season> ReadSeasons(SourceRow row, string column)
        {
            var seasons = SourceDocuments.Parse(row, column, SeasonsConverter) ?? new List<Season>();

            foreach (var season in seasons)
            {
                season.Images ??= new List<MediaCover.MediaCover>();
            }

            return seasons;
        }

        // Null when the series was not added with options (Sonarr clears them after the add).
        public static AddSeriesOptions ReadAddOptions(SourceRow row, string column)
        {
            return SourceDocuments.Parse(row, column, AddOptionsConverter);
        }

        // Null when the file has no media info yet.
        public static MediaInfoModel ReadMediaInfo(SourceRow row, string column)
        {
            return SourceDocuments.Parse(row, column, MediaInfoConverter);
        }

        public static QualityModel ReadQuality(SourceRow row, string column, IUpstreamCatalog catalog)
        {
            return SourceDocuments.Parse(CatalogRemapper.RemapQuality(row.GetJson(column), catalog), QualityConverter);
        }

        public static List<Language> ReadLanguages(SourceRow row, string column, IUpstreamCatalog catalog)
        {
            return SourceDocuments.Parse(CatalogRemapper.RemapLanguages(row.GetJson(column), catalog), LanguagesConverter) ?? new List<Language>();
        }
    }
}
