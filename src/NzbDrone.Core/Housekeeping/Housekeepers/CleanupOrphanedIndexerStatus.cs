using Dapper;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedIndexerStatus : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedIndexerStatus(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            using var mapper = _database.OpenConnection();

            // Prowlarr's indexer layer. The tables take the plain names in step 8.
            mapper.Execute(@"DELETE FROM ""PlattarrIndexerStatus""
                             WHERE ""Id"" IN (
                             SELECT ""PlattarrIndexerStatus"".""Id"" FROM ""PlattarrIndexerStatus""
                             LEFT OUTER JOIN ""PlattarrIndexers""
                             ON ""PlattarrIndexerStatus"".""ProviderId"" = ""PlattarrIndexers"".""Id""
                             WHERE ""PlattarrIndexers"".""Id"" IS NULL)");
        }
    }
}
