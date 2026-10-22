using System;
using System.Collections.Generic;
using NzbDrone.Core.Extras.Files;
using NzbDrone.Core.Extras.Metadata;
using NzbDrone.Core.Extras.Metadata.Files;
using NzbDrone.Core.Extras.Others;
using NzbDrone.Core.Extras.Subtitles;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Movies.Upstream
{
    // The files that sit next to a movie file: subtitles, metadata (NFO, images) and other
    // extras (docs/importer.md, "Write order", step 8).
    //
    // Each row points at a movie and, optionally, at the movie file it belongs to. A row whose
    // movie was not imported is skipped as CORRUPT. A MovieFileId of 0 (or NULL for metadata
    // files) means "not tied to a file" and stays that way; a non-zero MovieFileId whose file
    // was not imported is skipped as CORRUPT, because the target would show it attached to
    // whatever file later takes that ID.
    public class MovieExtrasStage : IImportStage
    {
        public const string SubtitleFilesTable = "SubtitleFiles";
        public const string ExtraFilesTable = "ExtraFiles";
        public const string MetadataFilesTable = "MetadataFiles";

        private readonly ISubtitleFileRepository _subtitles;
        private readonly IOtherExtraFileRepository _others;
        private readonly IMetadataFileRepository _metadataFiles;
        private readonly IEnumerable<IUpstreamCatalog> _catalogs;

        public MovieExtrasStage(
            ISubtitleFileRepository subtitles,
            IOtherExtraFileRepository others,
            IMetadataFileRepository metadataFiles,
            IEnumerable<IUpstreamCatalog> catalogs)
        {
            _subtitles = subtitles;
            _others = others;
            _metadataFiles = metadataFiles;
            _catalogs = catalogs;
        }

        public string Name => "Movie extra files";

        public int Order => ImportOrder.MovieExtras;

        public IReadOnlyList<SourceTableRef> Tables { get; } = new[]
        {
            new SourceTableRef(UpstreamApps.Radarr, SubtitleFilesTable),
            new SourceTableRef(UpstreamApps.Radarr, ExtraFilesTable),
            new SourceTableRef(UpstreamApps.Radarr, MetadataFilesTable)
        };

        public void Run(ImportSession session)
        {
            var snapshot = session.Snapshot(UpstreamApps.Radarr);

            if (snapshot == null)
            {
                return;
            }

            var catalog = _catalogs.For(UpstreamApps.Radarr);

            ImportSubtitles(session, snapshot, catalog);
            ImportOthers(session, snapshot);
            ImportMetadataFiles(session, snapshot);
        }

        private void ImportSubtitles(ImportSession session, SourceSnapshot snapshot, IUpstreamCatalog catalog)
        {
            var batch = new RowBatch<SubtitleFile>();

            foreach (var row in snapshot.Rows(SubtitleFilesTable))
            {
                var id = row.GetLong("Id");
                var path = row.GetString("RelativePath");

                try
                {
                    if (!TryLink(session, SubtitleFilesTable, row, id, path, out var movieId, out var movieFileId))
                    {
                        continue;
                    }

                    var file = new SubtitleFile
                    {
                        Language = (Language)CatalogRemapper.LanguageId(row.GetInt("Language"), catalog),
                        LanguageTags = MovieDocuments.ReadStrings(row, "LanguageTags"),
                        Copy = row.GetNullableInt("Copy") ?? 0,
                        Title = row.GetString("Title")
                    };

                    Fill(session, file, row, movieId, movieFileId);
                    batch.Add(id, file);
                }
                catch (Exception ex) when (SourceDocuments.IsBad(ex))
                {
                    session.SkipCorrupt(UpstreamApps.Radarr, SubtitleFilesTable, id, path, "Subtitle file could not be read: " + SourceDocuments.Describe(ex));
                }
            }

            batch.Write(session, UpstreamApps.Radarr, SubtitleFilesTable, _subtitles);
        }

        private void ImportOthers(ImportSession session, SourceSnapshot snapshot)
        {
            var batch = new RowBatch<OtherExtraFile>();

            foreach (var row in snapshot.Rows(ExtraFilesTable))
            {
                var id = row.GetLong("Id");
                var path = row.GetString("RelativePath");

                try
                {
                    if (!TryLink(session, ExtraFilesTable, row, id, path, out var movieId, out var movieFileId))
                    {
                        continue;
                    }

                    var file = new OtherExtraFile();

                    Fill(session, file, row, movieId, movieFileId);
                    batch.Add(id, file);
                }
                catch (Exception ex) when (SourceDocuments.IsBad(ex))
                {
                    session.SkipCorrupt(UpstreamApps.Radarr, ExtraFilesTable, id, path, "Extra file could not be read: " + SourceDocuments.Describe(ex));
                }
            }

            batch.Write(session, UpstreamApps.Radarr, ExtraFilesTable, _others);
        }

        private void ImportMetadataFiles(ImportSession session, SourceSnapshot snapshot)
        {
            var batch = new RowBatch<MetadataFile>();

            foreach (var row in snapshot.Rows(MetadataFilesTable))
            {
                var id = row.GetLong("Id");
                var path = row.GetString("RelativePath");

                try
                {
                    if (!TryLink(session, MetadataFilesTable, row, id, path, out var movieId, out var movieFileId))
                    {
                        continue;
                    }

                    var file = new MetadataFile
                    {
                        Hash = row.GetString("Hash"),

                        // Radarr's consumer names ("XbmcMetadata", ...) are class names the
                        // target keeps; they are copied, not mapped.
                        Consumer = row.GetString("Consumer"),
                        Type = (MetadataType)row.GetInt("Type")
                    };

                    Fill(session, file, row, movieId, movieFileId);
                    batch.Add(id, file);
                }
                catch (Exception ex) when (SourceDocuments.IsBad(ex))
                {
                    session.SkipCorrupt(UpstreamApps.Radarr, MetadataFilesTable, id, path, "Metadata file could not be read: " + SourceDocuments.Describe(ex));
                }
            }

            batch.Write(session, UpstreamApps.Radarr, MetadataFilesTable, _metadataFiles);
        }

        // Resolves the movie and the optional movie file; false (and the skip is recorded) when
        // either was not imported.
        private static bool TryLink(ImportSession session, string table, SourceRow row, long id, string path, out int movieId, out int? movieFileId)
        {
            movieId = 0;
            movieFileId = null;

            if (!session.IdMap.TryResolve(UpstreamApps.Radarr, MoviesStage.MoviesTable, row.GetLong("MovieId"), out var resolvedMovie))
            {
                session.SkipCorrupt(UpstreamApps.Radarr, table, id, path, "Refers to a movie that was not imported");
                return false;
            }

            movieId = (int)resolvedMovie;

            var sourceFileId = row.GetNullableLong("MovieFileId");

            if (sourceFileId == null)
            {
                return true;
            }

            if (sourceFileId == 0)
            {
                movieFileId = 0;
                return true;
            }

            if (!session.IdMap.TryResolve(UpstreamApps.Radarr, MoviesStage.MovieFilesTable, sourceFileId.Value, out var resolvedFile))
            {
                session.SkipCorrupt(UpstreamApps.Radarr, table, id, path, "Refers to a movie file that was not imported");
                return false;
            }

            movieFileId = (int)resolvedFile;
            return true;
        }

        private static void Fill(ImportSession session, ExtraFile file, SourceRow row, int movieId, int? movieFileId)
        {
            file.MovieId = movieId;
            file.MovieFileId = movieFileId;
            file.RelativePath = row.GetString("RelativePath");
            file.Extension = row.GetString("Extension");
            file.Added = row.GetNullableDateTime("Added") ?? session.ImportedAt;
            file.LastUpdated = row.GetNullableDateTime("LastUpdated") ?? file.Added;
        }
    }
}
