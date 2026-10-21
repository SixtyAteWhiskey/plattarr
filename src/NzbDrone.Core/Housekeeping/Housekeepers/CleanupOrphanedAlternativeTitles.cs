using Dapper;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedAlternativeTitles : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedAlternativeTitles(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""Movies_AlternativeTitles""
                             WHERE ""Id"" IN (
                             SELECT ""Movies_AlternativeTitles"".""Id"" FROM ""Movies_AlternativeTitles""
                             LEFT OUTER JOIN ""Movies_MovieMetadata""
                             ON ""Movies_AlternativeTitles"".""MovieMetadataId"" = ""Movies_MovieMetadata"".""Id""
                             WHERE ""Movies_MovieMetadata"".""Id"" IS NULL)");
        }
    }
}
