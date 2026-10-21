using Dapper;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedHistoryItems : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedHistoryItems(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            CleanupOrphanedByMovie();
        }

        private void CleanupOrphanedByMovie()
        {
            // Movie rows only: other media types point at their own tables.
            using var mapper = _database.OpenConnection();
            mapper.Execute($@"DELETE FROM ""History""
                             WHERE ""Id"" IN (
                             SELECT ""History"".""Id"" FROM ""History""
                             LEFT OUTER JOIN ""Movies_Movies""
                             ON ""History"".""MovieId"" = ""Movies_Movies"".""Id""
                             WHERE ""History"".""MediaKind"" = {(int)MediaKind.Movies}
                             AND ""Movies_Movies"".""Id"" IS NULL)");
        }
    }
}
