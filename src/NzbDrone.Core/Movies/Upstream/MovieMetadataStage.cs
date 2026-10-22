using System;
using System.Collections.Generic;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Movies.AlternativeTitles;
using NzbDrone.Core.Movies.Collections;
using NzbDrone.Core.Movies.Credits;
using NzbDrone.Core.Movies.Translations;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Movies.Upstream
{
    // Radarr's movie metadata and what hangs off it: alternative titles, translations, credits
    // and collections (docs/importer.md, "Mapping" and "Write order", steps 5 and 6).
    //
    // Metadata is a catalog of every title Radarr has looked up, not a library: all of it is
    // imported, including titles no movie refers to (the movies stage decides what is in the
    // library). A row that refers to metadata that was not imported (the metadata row was
    // corrupt) is skipped as CORRUPT, because the target has no foreign keys and a dangling ID
    // would only fail later, on a page load.
    public class MovieMetadataStage : IImportStage
    {
        public const string MetadataTable = "MovieMetadata";
        public const string AlternativeTitlesTable = "AlternativeTitles";
        public const string TranslationsTable = "MovieTranslations";
        public const string CreditsTable = "Credits";
        public const string CollectionsTable = "Collections";

        private readonly IMovieMetadataRepository _metadata;
        private readonly IAlternativeTitleRepository _alternativeTitles;
        private readonly IMovieTranslationRepository _translations;
        private readonly ICreditRepository _credits;
        private readonly IMovieCollectionRepository _collections;
        private readonly IEnumerable<IUpstreamCatalog> _catalogs;

        public MovieMetadataStage(
            IMovieMetadataRepository metadata,
            IAlternativeTitleRepository alternativeTitles,
            IMovieTranslationRepository translations,
            ICreditRepository credits,
            IMovieCollectionRepository collections,
            IEnumerable<IUpstreamCatalog> catalogs)
        {
            _metadata = metadata;
            _alternativeTitles = alternativeTitles;
            _translations = translations;
            _credits = credits;
            _collections = collections;
            _catalogs = catalogs;
        }

        public string Name => "Movie metadata";

        public int Order => ImportOrder.MovieMetadata;

        public IReadOnlyList<SourceTableRef> Tables { get; } = new[]
        {
            new SourceTableRef(UpstreamApps.Radarr, MetadataTable),
            new SourceTableRef(UpstreamApps.Radarr, AlternativeTitlesTable),
            new SourceTableRef(UpstreamApps.Radarr, TranslationsTable),
            new SourceTableRef(UpstreamApps.Radarr, CreditsTable),
            new SourceTableRef(UpstreamApps.Radarr, CollectionsTable)
        };

        public void Run(ImportSession session)
        {
            var snapshot = session.Snapshot(UpstreamApps.Radarr);

            if (snapshot == null)
            {
                return;
            }

            var catalog = _catalogs.For(UpstreamApps.Radarr);

            ImportMetadata(session, snapshot, catalog);
            ImportAlternativeTitles(session, snapshot);
            ImportTranslations(session, snapshot, catalog);
            ImportCredits(session, snapshot);
            ImportCollections(session, snapshot);
        }

        private void ImportMetadata(ImportSession session, SourceSnapshot snapshot, IUpstreamCatalog catalog)
        {
            var batch = new RowBatch<MovieMetadata>();

            foreach (var row in snapshot.Rows(MetadataTable))
            {
                var id = row.GetLong("Id");
                var title = row.GetString("Title");

                try
                {
                    batch.Add(id, ToMetadata(row, catalog));
                }
                catch (Exception ex) when (SourceDocuments.IsBad(ex))
                {
                    session.SkipCorrupt(UpstreamApps.Radarr, MetadataTable, id, title, "Movie metadata could not be read: " + SourceDocuments.Describe(ex));
                }
            }

            batch.Write(session, UpstreamApps.Radarr, MetadataTable, _metadata);
        }

        private static MovieMetadata ToMetadata(SourceRow row, IUpstreamCatalog catalog)
        {
            return new MovieMetadata
            {
                TmdbId = row.GetInt("TmdbId"),
                ImdbId = row.GetString("ImdbId"),
                Images = MovieDocuments.ReadImages(row, "Images"),
                Genres = MovieDocuments.ReadStrings(row, "Genres"),
                Keywords = MovieDocuments.ReadStrings(row, "Keywords"),
                Title = row.GetString("Title"),
                SortTitle = row.GetString("SortTitle"),
                CleanTitle = row.GetString("CleanTitle"),
                OriginalTitle = row.GetString("OriginalTitle"),
                CleanOriginalTitle = row.GetString("CleanOriginalTitle"),
                OriginalLanguage = (Language)CatalogRemapper.LanguageId(row.GetInt("OriginalLanguage"), catalog),
                Status = (MovieStatusType)row.GetInt("Status"),
                LastInfoSync = row.GetNullableDateTime("LastInfoSync"),
                Runtime = row.GetInt("Runtime"),
                InCinemas = row.GetNullableDateTime("InCinemas"),
                PhysicalRelease = row.GetNullableDateTime("PhysicalRelease"),
                DigitalRelease = row.GetNullableDateTime("DigitalRelease"),
                Year = row.GetNullableInt("Year") ?? 0,
                SecondaryYear = row.GetNullableInt("SecondaryYear"),
                Ratings = MovieDocuments.ReadRatings(row, "Ratings"),
                Recommendations = MovieDocuments.ReadInts(row, "Recommendations"),
                Certification = row.GetString("Certification"),
                YouTubeTrailerId = row.GetString("YouTubeTrailerId"),
                Studio = row.GetString("Studio"),
                Overview = row.GetString("Overview"),
                Website = row.GetString("Website"),
                Popularity = (float)(row.GetNullableDouble("Popularity") ?? 0),
                CollectionTmdbId = row.GetNullableInt("CollectionTmdbId") ?? 0,
                CollectionTitle = row.GetString("CollectionTitle")
            };
        }

        // The target's CleanTitle index on this table is not unique (Radarr's was), so two movies
        // that share an alternative title both keep it.
        private void ImportAlternativeTitles(ImportSession session, SourceSnapshot snapshot)
        {
            var batch = new RowBatch<AlternativeTitle>();

            foreach (var row in snapshot.Rows(AlternativeTitlesTable))
            {
                var id = row.GetLong("Id");
                var title = row.GetString("Title");

                if (!session.IdMap.TryResolve(UpstreamApps.Radarr, MetadataTable, row.GetLong("MovieMetadataId"), out var metadataId))
                {
                    session.SkipCorrupt(UpstreamApps.Radarr, AlternativeTitlesTable, id, title, "Refers to movie metadata that was not imported");
                    continue;
                }

                batch.Add(id, new AlternativeTitle
                {
                    Title = title,
                    CleanTitle = row.GetString("CleanTitle"),
                    SourceType = (SourceType)row.GetInt("SourceType"),
                    MovieMetadataId = (int)metadataId
                });
            }

            batch.Write(session, UpstreamApps.Radarr, AlternativeTitlesTable, _alternativeTitles);
        }

        private void ImportTranslations(ImportSession session, SourceSnapshot snapshot, IUpstreamCatalog catalog)
        {
            var batch = new RowBatch<MovieTranslation>();

            foreach (var row in snapshot.Rows(TranslationsTable))
            {
                var id = row.GetLong("Id");
                var title = row.GetString("Title");

                if (!session.IdMap.TryResolve(UpstreamApps.Radarr, MetadataTable, row.GetLong("MovieMetadataId"), out var metadataId))
                {
                    session.SkipCorrupt(UpstreamApps.Radarr, TranslationsTable, id, title, "Refers to movie metadata that was not imported");
                    continue;
                }

                try
                {
                    batch.Add(id, new MovieTranslation
                    {
                        MovieMetadataId = (int)metadataId,
                        Title = title,
                        CleanTitle = row.GetString("CleanTitle"),
                        Overview = row.GetString("Overview"),
                        Language = (Language)CatalogRemapper.LanguageId(row.GetInt("Language"), catalog)
                    });
                }
                catch (Exception ex) when (SourceDocuments.IsBad(ex))
                {
                    session.SkipCorrupt(UpstreamApps.Radarr, TranslationsTable, id, title, "Translation could not be read: " + SourceDocuments.Describe(ex));
                }
            }

            batch.Write(session, UpstreamApps.Radarr, TranslationsTable, _translations);
        }

        private void ImportCredits(ImportSession session, SourceSnapshot snapshot)
        {
            var batch = new RowBatch<Credit>();

            foreach (var row in snapshot.Rows(CreditsTable))
            {
                var id = row.GetLong("Id");
                var name = row.GetString("Name");

                if (!session.IdMap.TryResolve(UpstreamApps.Radarr, MetadataTable, row.GetLong("MovieMetadataId"), out var metadataId))
                {
                    session.SkipCorrupt(UpstreamApps.Radarr, CreditsTable, id, name, "Refers to movie metadata that was not imported");
                    continue;
                }

                try
                {
                    batch.Add(id, new Credit
                    {
                        CreditTmdbId = row.GetString("CreditTmdbId"),
                        PersonTmdbId = row.GetInt("PersonTmdbId"),
                        Name = name,
                        Images = MovieDocuments.ReadImages(row, "Images"),
                        Character = row.GetString("Character"),
                        Order = row.GetInt("Order"),
                        Job = row.GetString("Job"),
                        Department = row.GetString("Department"),
                        Type = (CreditType)row.GetInt("Type"),
                        MovieMetadataId = (int)metadataId
                    });
                }
                catch (Exception ex) when (SourceDocuments.IsBad(ex))
                {
                    session.SkipCorrupt(UpstreamApps.Radarr, CreditsTable, id, name, "Credit could not be read: " + SourceDocuments.Describe(ex));
                }
            }

            batch.Write(session, UpstreamApps.Radarr, CreditsTable, _credits);
        }

        private void ImportCollections(ImportSession session, SourceSnapshot snapshot)
        {
            var batch = new RowBatch<MovieCollection>();

            foreach (var row in snapshot.Rows(CollectionsTable))
            {
                var id = row.GetLong("Id");
                var title = row.GetString("Title");

                // The movie list reads every collection's profile; an ID the target does not
                // have would fail there.
                if (!session.IdMap.TryResolve(UpstreamApps.Radarr, ProfilesStage.QualityProfilesTable, row.GetLong("QualityProfileId"), out var profileId))
                {
                    session.SkipCorrupt(UpstreamApps.Radarr, CollectionsTable, id, title, "Refers to a quality profile that was not imported");
                    continue;
                }

                try
                {
                    var tags = TagRemapper.Remap(UpstreamApps.Radarr, row.GetJson("Tags"), session.IdMap, out var dropped);

                    var collection = new MovieCollection
                    {
                        TmdbId = row.GetInt("TmdbId"),
                        QualityProfileId = (int)profileId,
                        RootFolderPath = session.Paths.Map(row.GetString("RootFolderPath")),
                        MinimumAvailability = (MovieStatusType)row.GetInt("MinimumAvailability"),
                        SearchOnAdd = row.GetBool("SearchOnAdd"),
                        Title = title,
                        SortTitle = row.GetString("SortTitle"),
                        CleanTitle = row.GetString("CleanTitle"),
                        Overview = row.GetString("Overview"),
                        Images = MovieDocuments.ReadImages(row, "Images"),
                        Monitored = row.GetBool("Monitored"),
                        LastInfoSync = row.GetNullableDateTime("LastInfoSync"),
                        Added = row.GetNullableDateTime("Added") ?? session.ImportedAt,
                        Tags = new HashSet<int>(tags)
                    };

                    if (dropped > 0)
                    {
                        session.Warn($"Collection \"{title}\" (Radarr {id}): dropped {dropped} tag(s) that were not imported");
                    }

                    batch.Add(id, collection);
                }
                catch (Exception ex) when (SourceDocuments.IsBad(ex))
                {
                    session.SkipCorrupt(UpstreamApps.Radarr, CollectionsTable, id, title, "Collection could not be read: " + SourceDocuments.Describe(ex));
                }
            }

            batch.Write(session, UpstreamApps.Radarr, CollectionsTable, _collections);
        }
    }
}
