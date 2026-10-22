using System.Collections.Generic;
using NzbDrone.Core.Datastore.Converters;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Movies.Upstream
{
    // The JSON columns of Radarr's movie tables, read with the converters the target's
    // repositories use (the same ones TableMapping and MoviesTableMap register), so what a stage
    // writes is what the target reads back. A column that is only copied is parsed from its
    // stored text; quality and language columns hold IDs that differ between apps, so they are
    // rewritten through the catalog first. A document the target cannot read throws, and the
    // stage skips that one row as CORRUPT (SourceDocuments.IsBad).
    public static class MovieDocuments
    {
        private static readonly EmbeddedDocumentConverter<List<MediaCover.MediaCover>> ImagesConverter = new EmbeddedDocumentConverter<List<MediaCover.MediaCover>>();
        private static readonly EmbeddedDocumentConverter<List<string>> StringsConverter = new EmbeddedDocumentConverter<List<string>>();
        private static readonly EmbeddedDocumentConverter<List<int>> IntsConverter = new EmbeddedDocumentConverter<List<int>>();
        private static readonly EmbeddedDocumentConverter<Ratings> RatingsConverter = new EmbeddedDocumentConverter<Ratings>();
        private static readonly EmbeddedDocumentConverter<AddMovieOptions> AddOptionsConverter = new EmbeddedDocumentConverter<AddMovieOptions>();
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

        public static List<int> ReadInts(SourceRow row, string column)
        {
            return SourceDocuments.Parse(row, column, IntsConverter) ?? new List<int>();
        }

        public static Ratings ReadRatings(SourceRow row, string column)
        {
            return SourceDocuments.Parse(row, column, RatingsConverter) ?? new Ratings();
        }

        // Null when the movie was not added with options (or they were cleared after the add).
        public static AddMovieOptions ReadAddOptions(SourceRow row, string column)
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
