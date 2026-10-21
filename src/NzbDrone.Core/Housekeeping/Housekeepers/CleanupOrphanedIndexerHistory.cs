using Dapper;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    // Prowlarr's CleanupOrphanedHistoryItems, for the IndexerHistory table. Radarr's class of
    // that name cleans the movie History table.
    public class CleanupOrphanedIndexerHistory : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedIndexerHistory(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""IndexerHistory""
                             WHERE ""Id"" IN (
                             SELECT ""IndexerHistory"".""Id"" FROM ""IndexerHistory""
                             LEFT OUTER JOIN ""PlattarrIndexers""
                             ON ""IndexerHistory"".""IndexerId"" = ""PlattarrIndexers"".""Id""
                             WHERE ""PlattarrIndexers"".""Id"" IS NULL)");
        }
    }
}
