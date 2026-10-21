using Dapper;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedMovieMetadata : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedMovieMetadata(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""Movies_MovieMetadata""
                             WHERE ""Id"" IN (
                             SELECT ""Movies_MovieMetadata"".""Id"" FROM ""Movies_MovieMetadata""
                             LEFT OUTER JOIN ""Movies_Movies"" ON ""Movies_Movies"".""MovieMetadataId"" = ""Movies_MovieMetadata"".""Id""
                             LEFT OUTER JOIN ""Movies_Collections"" ON ""Movies_Collections"".""TmdbId"" = ""Movies_MovieMetadata"".""CollectionTmdbId""
                             LEFT OUTER JOIN ""Movies_ImportListMovies"" ON ""Movies_ImportListMovies"".""MovieMetadataId"" = ""Movies_MovieMetadata"".""Id""
                             WHERE ""Movies_Movies"".""Id"" IS NULL AND ""Movies_ImportListMovies"".""Id"" IS NULL AND ""Movies_Collections"".""Id"" IS NULL)");
        }
    }
}
