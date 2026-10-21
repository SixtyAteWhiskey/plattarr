using Dapper;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedMovieFiles : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedMovieFiles(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""Movies_MovieFiles""
                             WHERE ""Id"" IN (
                             SELECT ""Movies_MovieFiles"".""Id"" FROM ""Movies_MovieFiles""
                             LEFT OUTER JOIN ""Movies_Movies""
                             ON ""Movies_MovieFiles"".""Id"" = ""Movies_Movies"".""MovieFileId""
                             WHERE ""Movies_Movies"".""Id"" IS NULL)");
        }
    }
}
