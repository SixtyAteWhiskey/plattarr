using System;
using System.Collections.Generic;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Tv.Extras.Files;
using NzbDrone.Core.Tv.Extras.Metadata;
using NzbDrone.Core.Tv.Extras.Metadata.Files;
using NzbDrone.Core.Tv.Extras.Others;
using NzbDrone.Core.Tv.Extras.Subtitles;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Tv.Upstream
{
    // The files that sit next to an episode file or a series folder: subtitles, metadata (NFO,
    // images) and other extras (docs/importer.md, "From sonarr.db").
    //
    // Each row points at a series and, optionally, at the episode file it belongs to. A row whose
    // series was not imported is skipped as CORRUPT. An EpisodeFileId of 0 or NULL means "not tied
    // to a file" (series and season images) and stays that way; a non-zero EpisodeFileId whose
    // file was not imported is skipped as CORRUPT, because the target would show it attached to
    // whatever file later takes that ID.
    public class TvExtrasStage : IImportStage
    {
        public const string SubtitleFilesTable = "SubtitleFiles";
        public const string ExtraFilesTable = "ExtraFiles";
        public const string MetadataFilesTable = "MetadataFiles";

        private readonly ISubtitleFileRepository _subtitles;
        private readonly IOtherExtraFileRepository _others;
        private readonly IMetadataFileRepository _metadataFiles;
        private readonly IEnumerable<IUpstreamCatalog> _catalogs;

        public TvExtrasStage(
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

        public string Name => "TV extra files";

        public int Order => ImportOrder.TvExtras;

        public IReadOnlyList<SourceTableRef> Tables { get; } = new[]
        {
            new SourceTableRef(UpstreamApps.Sonarr, SubtitleFilesTable),
            new SourceTableRef(UpstreamApps.Sonarr, ExtraFilesTable),
            new SourceTableRef(UpstreamApps.Sonarr, MetadataFilesTable)
        };

        public void Run(ImportSession session)
        {
            var snapshot = session.Snapshot(UpstreamApps.Sonarr);

            if (snapshot == null)
            {
                return;
            }

            var catalog = _catalogs.For(UpstreamApps.Sonarr);

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
                    if (!TryLink(session, SubtitleFilesTable, row, id, path, out var seriesId, out var episodeFileId))
                    {
                        continue;
                    }

                    var file = new SubtitleFile
                    {
                        Language = (Language)CatalogRemapper.LanguageId(row.GetInt("Language"), catalog),
                        LanguageTags = TvDocuments.ReadStrings(row, "LanguageTags"),
                        Copy = row.GetNullableInt("Copy") ?? 0,
                        Title = row.GetString("Title")
                    };

                    Fill(session, file, row, seriesId, episodeFileId);
                    batch.Add(id, file);
                }
                catch (Exception ex) when (SourceDocuments.IsBad(ex))
                {
                    session.SkipCorrupt(UpstreamApps.Sonarr, SubtitleFilesTable, id, path, "Subtitle file could not be read: " + SourceDocuments.Describe(ex));
                }
            }

            batch.Write(session, UpstreamApps.Sonarr, SubtitleFilesTable, _subtitles);
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
                    if (!TryLink(session, ExtraFilesTable, row, id, path, out var seriesId, out var episodeFileId))
                    {
                        continue;
                    }

                    var file = new OtherExtraFile();

                    Fill(session, file, row, seriesId, episodeFileId);
                    batch.Add(id, file);
                }
                catch (Exception ex) when (SourceDocuments.IsBad(ex))
                {
                    session.SkipCorrupt(UpstreamApps.Sonarr, ExtraFilesTable, id, path, "Extra file could not be read: " + SourceDocuments.Describe(ex));
                }
            }

            batch.Write(session, UpstreamApps.Sonarr, ExtraFilesTable, _others);
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
                    if (!TryLink(session, MetadataFilesTable, row, id, path, out var seriesId, out var episodeFileId))
                    {
                        continue;
                    }

                    var file = new MetadataFile
                    {
                        Hash = row.GetString("Hash"),

                        // Sonarr's consumer names ("XbmcMetadata", ...) are class names the target
                        // keeps; they are copied, not mapped.
                        Consumer = row.GetString("Consumer"),
                        Type = (MetadataType)row.GetInt("Type")
                    };

                    Fill(session, file, row, seriesId, episodeFileId);
                    batch.Add(id, file);
                }
                catch (Exception ex) when (SourceDocuments.IsBad(ex))
                {
                    session.SkipCorrupt(UpstreamApps.Sonarr, MetadataFilesTable, id, path, "Metadata file could not be read: " + SourceDocuments.Describe(ex));
                }
            }

            batch.Write(session, UpstreamApps.Sonarr, MetadataFilesTable, _metadataFiles);
        }

        // Resolves the series and the optional episode file; false (and the skip is recorded) when
        // either was not imported.
        private static bool TryLink(ImportSession session, string table, SourceRow row, long id, string path, out int seriesId, out int? episodeFileId)
        {
            seriesId = 0;
            episodeFileId = null;

            if (!session.IdMap.TryResolve(UpstreamApps.Sonarr, SeriesStage.SeriesTable, row.GetLong("SeriesId"), out var resolvedSeries))
            {
                session.SkipCorrupt(UpstreamApps.Sonarr, table, id, path, "Refers to a series that was not imported");
                return false;
            }

            seriesId = (int)resolvedSeries;

            var sourceFileId = row.GetNullableLong("EpisodeFileId");

            if (sourceFileId == null)
            {
                return true;
            }

            if (sourceFileId == 0)
            {
                episodeFileId = 0;
                return true;
            }

            if (!session.IdMap.TryResolve(UpstreamApps.Sonarr, SeriesStage.EpisodeFilesTable, sourceFileId.Value, out var resolvedFile))
            {
                session.SkipCorrupt(UpstreamApps.Sonarr, table, id, path, "Refers to an episode file that was not imported");
                return false;
            }

            episodeFileId = (int)resolvedFile;
            return true;
        }

        private static void Fill(ImportSession session, ExtraFile file, SourceRow row, int seriesId, int? episodeFileId)
        {
            file.SeriesId = seriesId;
            file.EpisodeFileId = episodeFileId;
            file.SeasonNumber = row.GetNullableInt("SeasonNumber");
            file.RelativePath = row.GetString("RelativePath");
            file.Extension = row.GetString("Extension");
            file.Added = row.GetNullableDateTime("Added") ?? session.ImportedAt;
            file.LastUpdated = row.GetNullableDateTime("LastUpdated") ?? file.Added;
        }
    }
}
