using System;
using System.Collections.Generic;
using NzbDrone.Core.ImportLists.ImportExclusions;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Movies.Upstream
{
    // The movies the user told Radarr never to add again (docs/importer.md, "Mapping").
    // The rows refer to nothing else: a TMDb ID, a title and a year. Radarr's TmdbId is unique,
    // as is the target's.
    public class ImportExclusionsStage : IImportStage
    {
        public const string ImportExclusionsTable = "ImportExclusions";

        private readonly IImportListExclusionRepository _exclusions;

        public ImportExclusionsStage(IImportListExclusionRepository exclusions)
        {
            _exclusions = exclusions;
        }

        public string Name => "Import exclusions";

        public int Order => ImportOrder.ImportExclusions;

        public IReadOnlyList<SourceTableRef> Tables { get; } = new[]
        {
            new SourceTableRef(UpstreamApps.Radarr, ImportExclusionsTable)
        };

        public void Run(ImportSession session)
        {
            var snapshot = session.Snapshot(UpstreamApps.Radarr);

            if (snapshot == null)
            {
                return;
            }

            var batch = new RowBatch<ImportListExclusion>();

            foreach (var row in snapshot.Rows(ImportExclusionsTable))
            {
                var id = row.GetLong("Id");
                var title = row.GetString("MovieTitle");

                try
                {
                    batch.Add(id, new ImportListExclusion
                    {
                        TmdbId = row.GetInt("TmdbId"),
                        MovieTitle = title,
                        MovieYear = row.GetNullableInt("MovieYear") ?? 0
                    });
                }
                catch (Exception ex) when (SourceDocuments.IsBad(ex))
                {
                    session.SkipCorrupt(UpstreamApps.Radarr, ImportExclusionsTable, id, title, "Exclusion could not be read: " + SourceDocuments.Describe(ex));
                }
            }

            batch.Write(session, UpstreamApps.Radarr, ImportExclusionsTable, _exclusions);
        }
    }
}
