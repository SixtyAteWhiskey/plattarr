using Dapper;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedMovieMovieFileIds : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedMovieMovieFileIds(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"UPDATE ""Movies_Movies""
                             SET ""MovieFileId"" = 0
                             WHERE ""Id"" IN (
                             SELECT ""Movies_Movies"".""Id"" FROM ""Movies_Movies""
                             LEFT OUTER JOIN ""Movies_MovieFiles""
                             ON ""Movies_Movies"".""MovieFileId"" = ""Movies_MovieFiles"".""Id""
                             WHERE ""Movies_MovieFiles"".""Id"" IS NULL)");
        }
    }
}
