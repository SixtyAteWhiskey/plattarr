using Dapper;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    // IndexerOverrideService deletes rows with their indexer; this catches anything left behind.
    public class CleanupOrphanedIndexerOverrides : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedIndexerOverrides(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""IndexerOverrides""
                             WHERE ""Id"" IN (
                             SELECT ""IndexerOverrides"".""Id"" FROM ""IndexerOverrides""
                             LEFT OUTER JOIN ""Indexers""
                             ON ""IndexerOverrides"".""IndexerId"" = ""Indexers"".""Id""
                             WHERE ""Indexers"".""Id"" IS NULL)");
        }
    }
}
