using Dapper;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedSubtitleFiles : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedSubtitleFiles(IMainDatabase database)
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
            mapper.Execute(@"DELETE FROM ""Movies_SubtitleFiles""
                             WHERE ""Id"" IN (
                             SELECT ""Movies_SubtitleFiles"".""Id"" FROM ""Movies_SubtitleFiles""
                             LEFT OUTER JOIN ""Movies_Movies""
                             ON ""Movies_SubtitleFiles"".""MovieId"" = ""Movies_Movies"".""Id""
                             WHERE ""Movies_Movies"".""Id"" IS NULL)");
        }

        private void DeleteOrphanedByMovieFile()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""Movies_SubtitleFiles""
                             WHERE ""Id"" IN (
                             SELECT ""Movies_SubtitleFiles"".""Id"" FROM ""Movies_SubtitleFiles""
                             LEFT OUTER JOIN ""Movies_MovieFiles""
                             ON ""Movies_SubtitleFiles"".""MovieFileId"" = ""Movies_MovieFiles"".""Id""
                             WHERE ""Movies_SubtitleFiles"".""MovieFileId"" > 0
                             AND ""Movies_MovieFiles"".""Id"" IS NULL)");
        }
    }
}
