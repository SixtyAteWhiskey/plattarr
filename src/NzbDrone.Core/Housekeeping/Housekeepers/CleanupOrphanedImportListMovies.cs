using Dapper;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedImportListMovies : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedImportListMovies(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            CleanupOrphanedByImportLists();
            CleanupOrphanedByMovieMetadata();
        }

        private void CleanupOrphanedByImportLists()
        {
            using var mapper = _database.OpenConnection();

            mapper.Execute(@"DELETE FROM ""Movies_ImportListMovies""
                                 WHERE ""Id"" IN (
                                 SELECT ""Movies_ImportListMovies"".""Id""
                                 FROM ""Movies_ImportListMovies""
                                 LEFT OUTER JOIN ""Movies_ImportLists"" ON ""Movies_ImportListMovies"".""ListId"" = ""Movies_ImportLists"".""Id""
                                 WHERE ""Movies_ImportLists"".""Id"" IS NULL)");
        }

        private void CleanupOrphanedByMovieMetadata()
        {
            using var mapper = _database.OpenConnection();

            mapper.Execute(@"DELETE FROM ""Movies_ImportListMovies""
                                 WHERE ""Id"" IN (
                                 SELECT ""Movies_ImportListMovies"".""Id""
                                 FROM ""Movies_ImportListMovies""
                                 LEFT OUTER JOIN ""Movies_MovieMetadata"" ON ""Movies_ImportListMovies"".""MovieMetadataId"" = ""Movies_MovieMetadata"".""Id""
                                 WHERE ""Movies_MovieMetadata"".""Id"" IS NULL)");
        }
    }
}
