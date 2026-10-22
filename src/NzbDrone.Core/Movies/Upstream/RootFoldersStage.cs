using System;
using System.Collections.Generic;
using NzbDrone.Core.Movies.RootFolders;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Movies.Upstream
{
    // Radarr's root folders, written to Movies_RootFolders with the path map applied
    // (docs/importer.md, "Paths"). Nothing refers to a root folder by ID (movies, collections and
    // import lists store the path), so the IdMap entry only serves the report's CSV.
    public class RootFoldersStage : IImportStage
    {
        private const string SourceTable = "RootFolders";

        private readonly IRootFolderRepository _rootFolders;

        public RootFoldersStage(IRootFolderRepository rootFolders)
        {
            _rootFolders = rootFolders;
        }

        public string Name => "Movie root folders";

        public int Order => ImportOrder.RootFolders;

        public IReadOnlyList<SourceTableRef> Tables { get; } = new[]
        {
            new SourceTableRef(UpstreamApps.Radarr, SourceTable)
        };

        public void Run(ImportSession session)
        {
            var snapshot = session.Snapshot(UpstreamApps.Radarr);

            if (snapshot == null)
            {
                return;
            }

            var models = new List<RootFolder>();
            var sources = new List<(long Id, int Index)>();
            var byPath = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var row in snapshot.Rows(SourceTable))
            {
                var id = row.GetLong("Id");
                var path = row.GetString("Path");

                if (string.IsNullOrWhiteSpace(path))
                {
                    session.Skip(new ImportSkip(UpstreamApps.Radarr, SourceTable, id, null, SkipReason.Corrupt, "Root folder has no path"));
                    continue;
                }

                var mapped = session.Paths.Map(path);

                // Two source folders can map onto one path; the second is the first.
                if (!byPath.TryGetValue(mapped, out var index))
                {
                    index = models.Count;
                    byPath[mapped] = index;
                    models.Add(new RootFolder { Path = mapped });
                }

                sources.Add((id, index));
            }

            _rootFolders.InsertMany(models, session.Connection, session.Transaction);

            foreach (var (id, index) in sources)
            {
                session.IdMap.Record(UpstreamApps.Radarr, SourceTable, id, models[index].Id);
                session.Imported(UpstreamApps.Radarr, SourceTable);
            }
        }
    }
}
