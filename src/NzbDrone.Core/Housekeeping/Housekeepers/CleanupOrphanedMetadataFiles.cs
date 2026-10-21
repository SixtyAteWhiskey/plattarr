using Dapper;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedMetadataFiles : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedMetadataFiles(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            DeleteOrphanedByMovie();
            DeleteOrphanedByMovieFile();
            DeleteWhereMovieFileIsZero();
        }

        private void DeleteOrphanedByMovie()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""Movies_MetadataFiles""
                             WHERE ""Id"" IN (
                             SELECT ""Movies_MetadataFiles"".""Id"" FROM ""Movies_MetadataFiles""
                             LEFT OUTER JOIN ""Movies_Movies""
                             ON ""Movies_MetadataFiles"".""MovieId"" = ""Movies_Movies"".""Id""
                             WHERE ""Movies_Movies"".""Id"" IS NULL)");
        }

        private void DeleteOrphanedByMovieFile()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""Movies_MetadataFiles""
                             WHERE ""Id"" IN (
                             SELECT ""Movies_MetadataFiles"".""Id"" FROM ""Movies_MetadataFiles""
                             LEFT OUTER JOIN ""Movies_MovieFiles""
                             ON ""Movies_MetadataFiles"".""MovieFileId"" = ""Movies_MovieFiles"".""Id""
                             WHERE ""Movies_MetadataFiles"".""MovieFileId"" > 0
                             AND ""Movies_MovieFiles"".""Id"" IS NULL)");
        }

        private void DeleteWhereMovieFileIsZero()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""Movies_MetadataFiles""
                             WHERE ""Id"" IN (
                             SELECT ""Id"" FROM ""Movies_MetadataFiles""
                             WHERE ""Type"" IN (1, 2)
                             AND ""MovieFileId"" = 0)");
        }
    }
}
