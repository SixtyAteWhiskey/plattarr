using Dapper;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedExtraFiles : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedExtraFiles(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            DeleteOrphanedByMovie();
            DeleteOrphanedByMovieFile();
        }

        private void DeleteOrphanedByMovie()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""Movies_ExtraFiles""
                             WHERE ""Id"" IN (
                             SELECT ""Movies_ExtraFiles"".""Id"" FROM ""Movies_ExtraFiles""
                             LEFT OUTER JOIN ""Movies_Movies""
                             ON ""Movies_ExtraFiles"".""MovieId"" = ""Movies_Movies"".""Id""
                             WHERE ""Movies_Movies"".""Id"" IS NULL)");
        }

        private void DeleteOrphanedByMovieFile()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""Movies_ExtraFiles""
                             WHERE ""Id"" IN (
                             SELECT ""Movies_ExtraFiles"".""Id"" FROM ""Movies_ExtraFiles""
                             LEFT OUTER JOIN ""Movies_MovieFiles""
                             ON ""Movies_ExtraFiles"".""MovieFileId"" = ""Movies_MovieFiles"".""Id""
                             WHERE ""Movies_ExtraFiles"".""MovieFileId"" > 0
                             AND ""Movies_MovieFiles"".""Id"" IS NULL)");
        }
    }
}
