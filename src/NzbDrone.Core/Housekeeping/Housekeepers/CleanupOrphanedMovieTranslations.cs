using Dapper;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedMovieTranslations : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedMovieTranslations(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""Movies_MovieTranslations""
                             WHERE ""Id"" IN (
                             SELECT ""Movies_MovieTranslations"".""Id"" FROM ""Movies_MovieTranslations""
                             LEFT OUTER JOIN ""Movies_MovieMetadata""
                             ON ""Movies_MovieTranslations"".""MovieMetadataId"" = ""Movies_MovieMetadata"".""Id""
                             WHERE ""Movies_MovieMetadata"".""Id"" IS NULL)");
        }
    }
}
