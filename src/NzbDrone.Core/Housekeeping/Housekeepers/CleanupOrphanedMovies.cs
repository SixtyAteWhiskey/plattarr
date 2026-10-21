using Dapper;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedMovies : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedMovies(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""Movies_Movies""
                             WHERE ""Id"" IN (
                             SELECT ""Movies_Movies"".""Id"" FROM ""Movies_Movies""
                             LEFT OUTER JOIN ""Movies_MovieMetadata"" ON ""Movies_Movies"".""MovieMetadataId"" = ""Movies_MovieMetadata"".""Id""
                             WHERE ""Movies_MovieMetadata"".""Id"" IS NULL)");
        }
    }
}
