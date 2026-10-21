using Dapper;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedCredits : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedCredits(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""Movies_Credits""
                             WHERE ""Id"" IN (
                             SELECT ""Movies_Credits"".""Id"" FROM ""Movies_Credits""
                             LEFT OUTER JOIN ""Movies_MovieMetadata""
                             ON ""Movies_Credits"".""MovieMetadataId"" = ""Movies_MovieMetadata"".""Id""
                             WHERE ""Movies_MovieMetadata"".""Id"" IS NULL)");
        }
    }
}
