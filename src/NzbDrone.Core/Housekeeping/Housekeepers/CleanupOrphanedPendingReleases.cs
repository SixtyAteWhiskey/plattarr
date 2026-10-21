using Dapper;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedPendingReleases : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedPendingReleases(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            // Movie rows only: other media types point at their own tables.
            using var mapper = _database.OpenConnection();
            mapper.Execute($@"DELETE FROM ""PendingReleases""
                             WHERE ""Id"" IN (
                             SELECT ""PendingReleases"".""Id"" FROM ""PendingReleases""
                             LEFT OUTER JOIN ""Movies_Movies""
                             ON ""PendingReleases"".""ParentId"" = ""Movies_Movies"".""Id""
                             WHERE ""PendingReleases"".""MediaKind"" = {(int)MediaKind.Movies}
                             AND ""Movies_Movies"".""Id"" IS NULL)");
        }
    }
}
